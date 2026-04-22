using System;
using System.Collections.Generic;

namespace SmartVestFinancialAdvisor.Components.Services
{
    public static class SessionManager
    {
        private static readonly Dictionary<string, SessionData> _sessions = new();
        private static readonly object _lockObject = new();

        private class SessionData
        {
            public int UserId { get; set; }
            public string Email { get; set; } = string.Empty;
            public DateTime LastActivity { get; set; }
        }

        public static string CreateSession(int userId, string email)
        {
            lock (_lockObject)
            {
                string sessionKey = Guid.NewGuid().ToString("N");
                _sessions[sessionKey] = new SessionData
                {
                    UserId = userId,
                    Email = email,
                    LastActivity = DateTime.UtcNow
                };

                Console.WriteLine($"✅ Session created: {sessionKey} for user {userId}");
                return sessionKey;
            }
        }

        public static void SetActiveSession(string sessionKey)
        {
            lock (_lockObject)
            {
                if (_sessions.TryGetValue(sessionKey, out var session))
                {
                    session.LastActivity = DateTime.UtcNow;
                    Console.WriteLine($"✅ Session restored: {sessionKey} for user {session.UserId}");
                }
            }
        }

        public static int? GetUserId(string sessionKey)
        {
            lock (_lockObject)
            {
                if (_sessions.TryGetValue(sessionKey, out var session))
                {
                    return session.UserId;
                }
            }
            return null;
        }

        public static string GetEmail(string sessionKey)
        {
            lock (_lockObject)
            {
                if (_sessions.TryGetValue(sessionKey, out var session))
                {
                    return session.Email;
                }
            }
            return string.Empty;
        }

        public static bool HasActiveSession(string sessionKey)
        {
            lock (_lockObject)
            {
                return _sessions.ContainsKey(sessionKey);
            }
        }

        public static void ClearSession(string sessionKey)
        {
            lock (_lockObject)
            {
                if (_sessions.Remove(sessionKey))
                {
                    Console.WriteLine($"✅ Session cleared: {sessionKey}");
                }
            }
        }

        public static void CleanupExpiredSessions(int hoursToKeep = 24)
        {
            lock (_lockObject)
            {
                var expiredKeys = new List<string>();
                var cutoffTime = DateTime.UtcNow.AddHours(-hoursToKeep);

                foreach (var kvp in _sessions)
                {
                    if (kvp.Value.LastActivity < cutoffTime)
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }

                foreach (var key in expiredKeys)
                {
                    _sessions.Remove(key);
                    Console.WriteLine($"🗑️ Expired session removed: {key}");
                }
            }
        }
    }
}