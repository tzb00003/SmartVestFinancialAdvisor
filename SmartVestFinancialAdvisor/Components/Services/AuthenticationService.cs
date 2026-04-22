using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
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
        private readonly IJSRuntime _js;

        private bool _isLoggedIn;
        private int? _currentUserId;
        private string _currentEmail = string.Empty;
        private string _sessionKey = string.Empty;

        private static readonly string _circuitId = Guid.NewGuid().ToString();
        private string CircuitSessionKeyName => $"smartvest_session_{_circuitId}";

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

        public AuthenticationService(AppDbContext db, IJSRuntime js)
        {
            _db = db;
            _js = js;
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

                _sessionKey = SessionManager.CreateSession(user.Id, user.Email);

                try
                {
                    await _js.InvokeVoidAsync("localStorage.setItem", CircuitSessionKeyName, _sessionKey);
                    Console.WriteLine($"✅ Stored in localStorage with key: {CircuitSessionKeyName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  localStorage error: {ex.Message}");
                }

                CurrentUserId = user.Id;
                CurrentEmail = user.Email;
                IsLoggedIn = true;

                Console.WriteLine($"✅ LOGIN: {email}, SessionKey: {_sessionKey}, CircuitId: {_circuitId}");

                return new LoginResult
                {
                    Success = true,
                    Message = "Logged in successfully.",
                    UserId = user.Id,
                    Email = user.Email,
                    HasCompletedSurvey = user.HasCompletedSurvey,
                    SessionKey = _sessionKey
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

                _sessionKey = SessionManager.CreateSession(newUser.Id, newUser.Email);

                try
                {
                    await _js.InvokeVoidAsync("localStorage.setItem", CircuitSessionKeyName, _sessionKey);
                    Console.WriteLine($"✅ Stored in localStorage with key: {CircuitSessionKeyName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  localStorage error: {ex.Message}");
                }

                CurrentUserId = newUser.Id;
                CurrentEmail = newUser.Email;
                IsLoggedIn = true;

                Console.WriteLine($"✅ REGISTER: {email}, SessionKey: {_sessionKey}, CircuitId: {_circuitId}");

                return new RegisterResult
                {
                    Success = true,
                    Message = "Account created successfully.",
                    UserId = newUser.Id,
                    Email = newUser.Email,
                    SessionKey = _sessionKey
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
            Console.WriteLine($"🔴 LOGOUT: Clearing session {_sessionKey}");

            if (!string.IsNullOrEmpty(_sessionKey))
            {
                SessionManager.ClearSession(_sessionKey);
            }

            try
            {
                await _js.InvokeVoidAsync("localStorage.removeItem", CircuitSessionKeyName);
            }
            catch
            {
                Console.WriteLine("⚠️  localStorage not available");
            }

            CurrentUserId = null;
            CurrentEmail = string.Empty;
            IsLoggedIn = false;
            _sessionKey = string.Empty;
        }

        public async Task RestoreSessionAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_sessionKey))
                {
                    if (SessionManager.HasActiveSession(_sessionKey))
                    {
                        var userId = SessionManager.GetUserId(_sessionKey);
                        var email = SessionManager.GetEmail(_sessionKey);

                        if (userId.HasValue && !string.IsNullOrEmpty(email))
                        {
                            CurrentUserId = userId;
                            CurrentEmail = email;
                            IsLoggedIn = true;
                            Console.WriteLine($"✅ RESTORED from memory: {email}");
                            return;
                        }
                    }
                    _sessionKey = string.Empty;
                }

                string storedKey = null;
                try
                {
                    storedKey = await _js.InvokeAsync<string>("localStorage.getItem", CircuitSessionKeyName);
                    Console.WriteLine($"🔍 Retrieved from localStorage key '{CircuitSessionKeyName}': {storedKey}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  localStorage error: {ex.Message}");
                }

                if (!string.IsNullOrEmpty(storedKey))
                {
                    if (SessionManager.HasActiveSession(storedKey))
                    {
                        var userId = SessionManager.GetUserId(storedKey);
                        var email = SessionManager.GetEmail(storedKey);

                        if (userId.HasValue && !string.IsNullOrEmpty(email))
                        {
                            _sessionKey = storedKey;
                            CurrentUserId = userId;
                            CurrentEmail = email;
                            IsLoggedIn = true;
                            Console.WriteLine($"✅ RESTORED from localStorage: {email}");
                            return;
                        }
                    }

                    try
                    {
                        await _js.InvokeVoidAsync("localStorage.removeItem", CircuitSessionKeyName);
                    }
                    catch { }

                    Console.WriteLine($"⚠️  Stored session key is invalid");
                }

                IsLoggedIn = false;
                CurrentUserId = null;
                CurrentEmail = string.Empty;
                Console.WriteLine("⚠️  No valid session found");
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