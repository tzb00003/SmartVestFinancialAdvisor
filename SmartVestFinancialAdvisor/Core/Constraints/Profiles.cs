namespace SmartVestFinancialAdvisor.Core.Constraints
{
    /// <summary>
    /// Input model for Builder.
    /// </summary>
    public class FinancialProfile
    {
        public decimal MonthlyIncome { get; set; }
        public decimal Savings { get; set; }
        public decimal MonthlyDebt { get; set; }
        public decimal RiskTolerance { get; set; } // e.g., 0-1
    }
}