using System.Collections.Generic;
using SmartVestFinancialAdvisor.Core.Financial;

namespace SmartVestFinancialAdvisor.Core.Constraints
{
    /// <summary>
    /// The input model for the portfolio <see cref="Builder"/>.
    /// Contains both financial data and demographic information for scoring.
    /// </summary>
    public class FinancialProfile
    {
        /// <summary>Gross monthly income.</summary>
        public decimal MonthlyIncome { get; set; }

        /// <summary>Total current liquid savings.</summary>
        public decimal Savings { get; set; }

        /// <summary>Total debt payments.</summary>
        public decimal Debt { get; set; }

        /// <summary>Estimated monthly living expenses.</summary>
        public decimal MonthlyExpense { get; set; }

        /// <summary>Self-reported risk tolerance (0.0 = Conservative, 1.0 = Aggressive).</summary>
        public decimal RiskTolerance { get; set; }

        /// <summary>Optional list of detailed financial items (assets/liabilities).</summary>
        public List<FinancialItem>? Items { get; set; }

        /// <summary>Client's age for benchmark comparison.</summary>
        public int Age { get; set; }

        /// <summary>Client's state of residence (2-letter code).</summary>
        public string? LocationState { get; set; }

        /// <summary>Client's gender for specific benchmark comparison.</summary>
        public SmartVestFinancialAdvisor.Core.Benchmarks.Gender? Gender { get; set; }
    }

}
