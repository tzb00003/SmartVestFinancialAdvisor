using System;
using System.Collections.Generic;

namespace SmartVestFinancialAdvisor.Data
{
    public class User
    {
        public int Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

        public DateTime? LastSurveySubmitAt { get; set; }

        public bool HasCompletedSurvey { get; set; } = false;

        public ICollection<SurveySubmission> SurveySubmissions { get; set; } = new List<SurveySubmission>();

        public ICollection<SurveyResult> SurveyResults { get; set; } = new List<SurveyResult>();
    }
}