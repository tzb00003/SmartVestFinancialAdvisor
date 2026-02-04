namespace SmartVestFinancialAdvisor.Core.Scoring
{
    /// <summary>
    /// Represents a client’s financial profile.
    /// </summary>
    public class ClientProfile
    {
        public decimal MonthlyIncome { get; set; }
        public decimal Savings { get; set; }
        public decimal MonthlyDebt { get; set; }
    }
}