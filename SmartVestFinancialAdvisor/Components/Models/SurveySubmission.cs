using System;

namespace SmartVestFinancialAdvisor.Data
{
    public class SurveySubmission
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string EncryptedData { get; set; } = string.Empty;

        public string EncryptionIv { get; set; } = string.Empty;

        public string DataHash { get; set; } = string.Empty;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public int Version { get; set; } = 1;

        public User? User { get; set; }
    }
}