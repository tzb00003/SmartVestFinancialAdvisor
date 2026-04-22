using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SmartVestFinancialAdvisor.Components.Services
{
    public interface IEncryptionService
    {
        EncryptionResult Encrypt(string plaintext);

        string Decrypt(EncryptionData encryptionData);
    }

    public sealed class EncryptionService : IEncryptionService
    {
        private readonly byte[] _key;

        public EncryptionService(IConfiguration configuration)
        {
            var keyString = configuration["Encryption:Key"]
                ?? throw new InvalidOperationException(
                    "Encryption:Key not found in configuration. Add it to appsettings.json");

            _key = HexStringToByteArray(keyString);

            if (_key.Length != 32)
            {
                throw new InvalidOperationException(
                    $"Encryption key must be 32 bytes (256 bits), got {_key.Length} bytes. " +
                    $"Generate with: openssl rand -hex 32");
            }
        }

        public EncryptionResult Encrypt(string plaintext)
        {
            try
            {
                using (var aes = new AesGcm(_key))
                {
                    byte[] nonce = new byte[12];
                    using (var rng = RandomNumberGenerator.Create())
                    {
                        rng.GetBytes(nonce);
                    }

                    byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

                    byte[] ciphertext = new byte[plaintextBytes.Length];

                    byte[] tag = new byte[16];

                    aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

                    byte[] encryptedData = new byte[ciphertext.Length + tag.Length];
                    Buffer.BlockCopy(ciphertext, 0, encryptedData, 0, ciphertext.Length);
                    Buffer.BlockCopy(tag, 0, encryptedData, ciphertext.Length, tag.Length);

                    return new EncryptionResult
                    {
                        EncryptedDataBase64 = Convert.ToBase64String(encryptedData),
                        IvBase64 = Convert.ToBase64String(nonce),
                        DataHash = ComputeHash(plaintext)
                    };
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Encryption failed: " + ex.Message, ex);
            }
        }

        public string Decrypt(EncryptionData encryptionData)
        {
            try
            {
                using (var aes = new AesGcm(_key))
                {
                    byte[] encryptedBytes = Convert.FromBase64String(encryptionData.EncryptedDataBase64);
                    byte[] nonce = Convert.FromBase64String(encryptionData.IvBase64);

                    int ciphertextLength = encryptedBytes.Length - 16;
                    byte[] ciphertext = new byte[ciphertextLength];
                    byte[] tag = new byte[16];

                    Buffer.BlockCopy(encryptedBytes, 0, ciphertext, 0, ciphertextLength);
                    Buffer.BlockCopy(encryptedBytes, ciphertextLength, tag, 0, 16);

                    byte[] plaintext = new byte[ciphertext.Length];

                    aes.Decrypt(nonce, ciphertext, tag, plaintext);

                    string result = Encoding.UTF8.GetString(plaintext);

                    if (!string.IsNullOrEmpty(encryptionData.DataHash))
                    {
                        string computedHash = ComputeHash(result);
                        if (computedHash != encryptionData.DataHash)
                        {
                            throw new InvalidOperationException(
                                "Data integrity check failed. Encrypted data may be corrupted.");
                        }
                    }

                    return result;
                }
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    "Decryption failed. Data may be corrupted or encrypted with wrong key: " + ex.Message, ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Decryption failed: " + ex.Message, ex);
            }
        }

        private static string ComputeHash(string plaintext)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                byte[] hashBytes = sha256.ComputeHash(plaintextBytes);
                return Convert.ToHexString(hashBytes);
            }
        }

        private static byte[] HexStringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
    }

    public sealed class EncryptionResult
    {
        public string EncryptedDataBase64 { get; set; } = string.Empty;

        public string IvBase64 { get; set; } = string.Empty;

        public string DataHash { get; set; } = string.Empty;
    }

    public sealed class EncryptionData
    {
        public string EncryptedDataBase64 { get; set; } = string.Empty;

        public string IvBase64 { get; set; } = string.Empty;

        public string? DataHash { get; set; }
    }
}