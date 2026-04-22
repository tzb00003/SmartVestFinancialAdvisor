using System;

namespace SmartVestFinancialAdvisor.Data
{
    public class SurveyResult
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int SurveySubmissionId { get; set; }

        public decimal Score { get; set; }

        public string PortfolioJson { get; set; } = string.Empty;

        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }

        public SurveySubmission? SurveySubmission { get; set; }
    }
}