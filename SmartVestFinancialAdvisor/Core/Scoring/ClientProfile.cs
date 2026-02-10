using System.Collections.Generic;
using SmartVestFinancialAdvisor.Core.Financial;

namespace SmartVestFinancialAdvisor.Core.Scoring
{
    /// <summary>
    /// Represents a client’s financial profile.
    /// </summary>
    public class ClientProfile
    {
        public decimal MonthlyIncome { get; set; }
        public decimal Savings { get; set; }
        public decimal MonthlyExpense { get; set; }

        // Demographic Data
        public decimal MonthlyDebt { get; set; }
        public int Age { get; set; }
        public string? LocationState { get; set; }
        public SmartVestFinancialAdvisor.Core.Benchmarks.Gender? Gender { get; set; }
        public List<FinancialItem>? Items { get; set; }
    }
}