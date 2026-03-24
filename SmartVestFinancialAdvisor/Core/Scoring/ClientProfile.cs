using System.Collections.Generic;
using SmartVestFinancialAdvisor.Core.Financial;

namespace SmartVestFinancialAdvisor.Core.Scoring
{
    /// <summary>
    /// Represents a detailed snapshot of a client's financial and demographic situation.
    /// This is the primary input for the scoring and analysis agents.
    /// </summary>
    public class ClientProfile
    {
        /// <summary>Monthly gross income.</summary>
        public decimal MonthlyIncome { get; set; }

        /// <summary>Total liquid savings.</summary>
        public decimal Savings { get; set; }

        /// <summary>Estimated monthly living expenses.</summary>
        public decimal MonthlyExpense { get; set; }

        /// <summary>Total debt payments.</summary>
        public decimal Debt { get; set; }

        /// <summary>Client's age.</summary>
        public int Age { get; set; }

        /// <summary>State of residence (e.g., "CA").</summary>
        public string? LocationState { get; set; }

        /// <summary>Client's gender for specific benchmark comparison.</summary>
        public SmartVestFinancialAdvisor.Core.Benchmarks.Gender? Gender { get; set; }

        /// <summary>Collection of specific financial items (assets, loans, etc).</summary>
        public List<FinancialItem>? Items { get; set; }
    }
}