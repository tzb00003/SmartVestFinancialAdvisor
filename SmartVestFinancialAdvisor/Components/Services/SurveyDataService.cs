using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartVestFinancialAdvisor.Components.Models;
using SmartVestFinancialAdvisor.Data;

namespace SmartVestFinancialAdvisor.Components.Services
{
    public interface ISurveyDataService
    {
        Task<SurveySubmission> SaveSurveyAsync(int userId, FinancialSurveyModel model);

        Task<FinancialSurveyModel?> LoadLatestSurveyAsync(int userId);

        Task<SurveyResult?> GetLatestResultAsync(int userId);

        Task<SurveyResult> SaveResultAsync(int userId, int surveySubmissionId,
            decimal score, string portfolioJson);

        Task DeleteOldSubmissionsAsync(int userId, int keepCount = 5);
    }

    public sealed class SurveyDataService : ISurveyDataService
    {
        private readonly AppDbContext _db;
        private readonly IEncryptionService _encryption;

        public SurveyDataService(AppDbContext db, IEncryptionService encryption)
        {
            _db = db;
            _encryption = encryption;
        }

        public async Task<SurveySubmission> SaveSurveyAsync(int userId, FinancialSurveyModel model)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is null)
                throw new InvalidOperationException($"User {userId} not found.");

            try
            {
                string surveyJson = JsonSerializer.Serialize(model, new JsonSerializerOptions
                {
                    WriteIndented = false
                });

                var encryptionResult = _encryption.Encrypt(surveyJson);

                var submission = new SurveySubmission
                {
                    UserId = userId,
                    EncryptedData = encryptionResult.EncryptedDataBase64,
                    EncryptionIv = encryptionResult.IvBase64,
                    DataHash = encryptionResult.DataHash,
                    SubmittedAt = DateTime.UtcNow,
                    Version = 1
                };

                _db.SurveySubmissions.Add(submission);

                user.LastSurveySubmitAt = DateTime.UtcNow;
                user.HasCompletedSurvey = true;
                _db.Users.Update(user);

                await _db.SaveChangesAsync();

                return submission;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to save survey data.", ex);
            }
        }

        public async Task<FinancialSurveyModel?> LoadLatestSurveyAsync(int userId)
        {
            try
            {
                var submission = await _db.SurveySubmissions
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.SubmittedAt)
                    .FirstOrDefaultAsync();

                if (submission is null)
                    return null;

                var encryptionData = new EncryptionData
                {
                    EncryptedDataBase64 = submission.EncryptedData,
                    IvBase64 = submission.EncryptionIv,
                    DataHash = submission.DataHash
                };

                string decryptedJson = _encryption.Decrypt(encryptionData);

                var model = JsonSerializer.Deserialize<FinancialSurveyModel>(decryptedJson)
                    ?? throw new InvalidOperationException("Failed to deserialize survey data.");

                return model;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to load survey data.", ex);
            }
        }

        public async Task<SurveyResult?> GetLatestResultAsync(int userId)
        {
            return await _db.SurveyResults
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.ComputedAt)
                .FirstOrDefaultAsync();
        }
        public async Task<SurveyResult> SaveResultAsync(int userId, int surveySubmissionId,
            decimal score, string portfolioJson)
        {
            var result = new SurveyResult
            {
                UserId = userId,
                SurveySubmissionId = surveySubmissionId,
                Score = score,
                PortfolioJson = portfolioJson,
                ComputedAt = DateTime.UtcNow
            };

            _db.SurveyResults.Add(result);
            await _db.SaveChangesAsync();

            return result;
        }

        public async Task DeleteOldSubmissionsAsync(int userId, int keepCount = 5)
        {
            var allSubmissions = await _db.SurveySubmissions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SubmittedAt)
                .ToListAsync();

            if (allSubmissions.Count > keepCount)
            {
                var toDelete = allSubmissions.Skip(keepCount).ToList();
                _db.SurveySubmissions.RemoveRange(toDelete);
                await _db.SaveChangesAsync();
            }
        }
    }
}