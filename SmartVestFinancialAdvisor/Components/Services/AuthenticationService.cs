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

                CurrentUserId = user.Id;
                CurrentEmail = user.Email;
                IsLoggedIn = true;

                SessionManager.SetSession(user.Id, user.Email);

                return new LoginResult
                {
                    Success = true,
                    Message = "Logged in successfully.",
                    UserId = user.Id,
                    Email = user.Email,
                    HasCompletedSurvey = user.HasCompletedSurvey
                };
            }
            catch (Exception ex)
            {
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

                CurrentUserId = newUser.Id;
                CurrentEmail = newUser.Email;
                IsLoggedIn = true;

                SessionManager.SetSession(newUser.Id, newUser.Email);

                return new RegisterResult
                {
                    Success = true,
                    Message = "Account created successfully.",
                    UserId = newUser.Id,
                    Email = newUser.Email
                };
            }
            catch (Exception ex)
            {
                return new RegisterResult
                {
                    Success = false,
                    Message = $"Registration failed: {ex.Message}"
                };
            }
        }

        public async Task LogoutAsync()
        {
            CurrentUserId = null;
            CurrentEmail = string.Empty;
            IsLoggedIn = false;

            SessionManager.ClearSession();

            await Task.CompletedTask;
        }

        public async Task RestoreSessionAsync()
        {
            try
            {
                if (SessionManager.HasActiveSession())
                {
                    var userId = SessionManager.UserId;
                    var email = SessionManager.Email;

                    var user = await _db.Users.FindAsync(userId);

                    if (user is not null)
                    {
                        CurrentUserId = user.Id;
                        CurrentEmail = user.Email;
                        IsLoggedIn = true;

                        Console.WriteLine($"✅ Session restored: UserId={user.Id}, Email={user.Email}");
                    }
                    else
                    {
                        await LogoutAsync();
                    }
                }
                else
                {
                    IsLoggedIn = false;
                    CurrentUserId = null;
                    CurrentEmail = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to restore session: {ex.Message}");
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
    }

    public sealed class RegisterResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string Email { get; set; } = string.Empty;
    }
}