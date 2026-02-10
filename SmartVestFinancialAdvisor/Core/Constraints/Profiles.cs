using System.Collections.Generic;
using SmartVestFinancialAdvisor.Core.Financial;

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
        public decimal MonthlyExpense { get; set; }
        public decimal RiskTolerance { get; set; } // e.g., 0-1
        public List<FinancialItem>? Items { get; set; }

        // Demographic Data for Scoring
        public int Age { get; set; }
        public string? LocationState { get; set; }
        public SmartVestFinancialAdvisor.Core.Benchmarks.Gender? Gender { get; set; }
    }
}
