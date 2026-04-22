using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartVestFinancialAdvisor.Data;

namespace SmartVestFinancialAdvisor.Components.Services
{
    public interface IAuthenticationService
    {
        bool IsLoggedIn { get; }

        int? CurrentUserId { get; }

        string CurrentEmail { get; }

        event Action? OnStateChanged;

        Task<LoginResult> LoginAsync(string email, string password);

        Task<RegisterResult> RegisterAsync(string email, string password);

        Task LogoutAsync();

        Task RestoreSessionAsync();
    }

    public sealed class AuthenticationService : IAuthenticationService
    {
        private readonly AppDbContext _db;

        private bool _isLoggedIn;
        private int? _currentUserId;
        private string _currentEmail = string.Empty;

        // ✅ CRITICAL: Store current session key statically per circuit
        // This survives component reloads and page navigations
        private static string _circuitSessionKey = string.Empty;

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            private set
            {
                if (_isLoggedIn != value)
                {
                    _isLoggedIn = value;
                    OnStateChanged?.Invoke();
                }
            }
        }

        public int? CurrentUserId
        {
            get => _currentUserId;
            private set
            {
                if (_currentUserId != value)
                {
                    _currentUserId = value;
                    OnStateChanged?.Invoke();
                }
            }
        }

        public string CurrentEmail
        {
            get => _currentEmail;
            private set
            {
                if (_currentEmail != value)
                {
                    _currentEmail = value;
                    OnStateChanged?.Invoke();
                }
            }
        }

        public event Action? OnStateChanged;

        public AuthenticationService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<LoginResult> LoginAsync(string email, string password)
        {
            try
            {
                var user = await _db.Users
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (user is null)
                    return new LoginResult { Success = false, Message = "Invalid email or password." };

                if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                    return new LoginResult { Success = false, Message = "Invalid email or password." };

                user.LastLoginAt = DateTime.UtcNow;
                _db.Users.Update(user);
                await _db.SaveChangesAsync();

                // ✅ Create session and STORE it statically
                _circuitSessionKey = SessionManager.CreateSession(user.Id, user.Email);

                CurrentUserId = user.Id;
                CurrentEmail = user.Email;
                IsLoggedIn = true;

                Console.WriteLine($"✅ LOGIN: {email} with key {_circuitSessionKey}");

                return new LoginResult
                {
                    Success = true,
                    Message = "Logged in successfully.",
                    UserId = user.Id,
                    Email = user.Email,
                    HasCompletedSurvey = user.HasCompletedSurvey,
                    SessionKey = _circuitSessionKey
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ LOGIN FAILED: {ex.Message}");
                return new LoginResult
                {
                    Success = false,
                    Message = $"Login failed: {ex.Message}"
                };
            }
        }

        public async Task<RegisterResult> RegisterAsync(string email, string password)
        {
            try
            {
                var existingUser = await _db.Users
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (existingUser is not null)
                    return new RegisterResult
                    {
                        Success = false,
                        Message = "An account with this email already exists."
                    };

                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

                var newUser = new User
                {
                    Email = email,
                    PasswordHash = hashedPassword,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Users.Add(newUser);
                await _db.SaveChangesAsync();

                // ✅ Create session and STORE it statically
                _circuitSessionKey = SessionManager.CreateSession(newUser.Id, newUser.Email);

                CurrentUserId = newUser.Id;
                CurrentEmail = newUser.Email;
                IsLoggedIn = true;

                Console.WriteLine($"✅ REGISTER: {email} with key {_circuitSessionKey}");

                return new RegisterResult
                {
                    Success = true,
                    Message = "Account created successfully.",
                    UserId = newUser.Id,
                    Email = newUser.Email,
                    SessionKey = _circuitSessionKey
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ REGISTER FAILED: {ex.Message}");
                return new RegisterResult
                {
                    Success = false,
                    Message = $"Registration failed: {ex.Message}"
                };
            }
        }

        public async Task LogoutAsync()
        {
            Console.WriteLine($"🔴 LOGOUT: Clearing {_circuitSessionKey}");

            if (!string.IsNullOrEmpty(_circuitSessionKey))
            {
                SessionManager.ClearSession(_circuitSessionKey);
                _circuitSessionKey = string.Empty;
            }

            CurrentUserId = null;
            CurrentEmail = string.Empty;
            IsLoggedIn = false;

            await Task.CompletedTask;
        }

        public async Task RestoreSessionAsync()
        {
            try
            {
                // ✅ ALWAYS read from the static circuit key first
                if (string.IsNullOrEmpty(_circuitSessionKey))
                {
                    IsLoggedIn = false;
                    Console.WriteLine("⚠️  No session key in circuit");
                    return;
                }

                // ✅ Verify session still exists in SessionManager
                if (!SessionManager.HasActiveSession(_circuitSessionKey))
                {
                    IsLoggedIn = false;
                    _circuitSessionKey = string.Empty;
                    Console.WriteLine("⚠️  Session key invalid");
                    return;
                }

                // ✅ Restore from SessionManager
                var userId = SessionManager.GetUserId(_circuitSessionKey);
                var email = SessionManager.GetEmail(_circuitSessionKey);

                if (userId.HasValue && !string.IsNullOrEmpty(email))
                {
                    CurrentUserId = userId;
                    CurrentEmail = email;
                    IsLoggedIn = true;
                    Console.WriteLine($"✅ RESTORED: {email}");
                }
                else
                {
                    IsLoggedIn = false;
                    Console.WriteLine("⚠️  Could not read session data");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ RESTORE ERROR: {ex.Message}");
                IsLoggedIn = false;
            }
        }
    }

    public sealed class LoginResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public bool HasCompletedSurvey { get; set; }
        public string SessionKey { get; set; } = string.Empty;
    }

    public sealed class RegisterResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string SessionKey { get; set; } = string.Empty;
    }
}