namespace SmartVestFinancialAdvisor.Core.Financial
{
    /// <summary>
    /// Represents an individual financial record such as a savings account, loan, or investment.
    /// </summary>
    public class FinancialItem
    {
        /// <summary>Label for the item (e.g., "Chase Savings", "Student Loan").</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>The current balance or value of the item.</summary>
        public decimal Amount { get; set; }

        /// <summary>The monthly payment for the item.</summary>
        public decimal MonthlyPayment { get; set; }

        /// <summary>The associated interest rate (0.05 for 5%).</summary>
        public decimal InterestRate { get; set; }

        /// <summary>True if this is a liability (debt), false if it is an asset.</summary>
        public bool IsDebt { get; set; }

        /// <summary>
        /// True if this is a retirement-specific account (e.g., 401k, IRA).
        /// Used to separate long-term wealth from short-term liquidity.
        /// </summary>
        public bool IsRetirement { get; set; }
    }
}
