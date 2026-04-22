using System;

namespace SmartVestFinancialAdvisor.Components.Services
{
    public static class SessionManager
    {
        private static int? _userId;
        private static string _email = string.Empty;
        private static bool _isAuthenticated = false;

        public static int? UserId => _userId;
        public static string Email => _email;
        public static bool IsAuthenticated => _isAuthenticated;

        public static void SetSession(int userId, string email)
        {
            _userId = userId;
            _email = email;
            _isAuthenticated = true;

            Console.WriteLine($"✅ Session set: UserId={userId}, Email={email}");
        }

        public static void ClearSession()
        {
            _userId = null;
            _email = string.Empty;
            _isAuthenticated = false;

            Console.WriteLine("✅ Session cleared");
        }

        public static bool HasActiveSession()
        {
            return _isAuthenticated && _userId.HasValue && !string.IsNullOrEmpty(_email);
        }
    }
}