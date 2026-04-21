using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace SmartVestFinancialAdvisor.Components.Models
{
    /// <summary>
    /// User-entered financial survey data with annotations for MudForm validation.
    /// </summary>
    public sealed class FinancialSurveyModel
    {
        [Required, Range(0, double.MaxValue)]
        public decimal? MonthlyIncome { get; set; }

        [Required, Range(0, double.MaxValue)]
        public decimal? Savings { get; set; }

        /// <summary>Total monthly debt payments (if the user enters a single combined figure).</summary>
        [Required, Range(0, double.MaxValue)]
        public decimal? Debt { get; set; }

        [Required, Range(0, double.MaxValue)]
        public decimal? MonthlyExpense { get; set; }

        public RiskLevel? RiskLevel { get; set; }

        [Required, Range(18, 120)]
        public int? Age { get; set; }

        [Required]
        public string? State { get; set; }

        /// <summary>
        /// Assets & liabilities entered by the user.
        /// Start EMPTY so the form can be valid without any item rows.
        /// </summary>
        public ObservableCollection<FinancialItem> Items { get; } = new();
    }

    public sealed class FinancialItem
    {
        // NOTE: Removed [Required] so a blank row won't invalidate the whole form.
        // Keep UI-level Required="true" in the table if you want to enforce label when a row is edited.
        public string? Label { get; set; }

        /// <summary>Current balance/value.</summary>
        [Range(0, double.MaxValue)]
        public decimal? Amount { get; set; }

        /// <summary>Monthly payment for this item (if applicable).</summary>
        [Range(0, double.MaxValue)]
        public decimal? MonthlyPayment { get; set; }

        /// <summary>Interest rate as decimal (0.05 for 5%).</summary>
        [Range(0, 1)]
        public decimal? InterestRate { get; set; }

        /// <summary>True if liability (debt).</summary>
        public bool IsDebt { get; set; }

        /// <summary>True if retirement-specific account (disabled when IsDebt = true).</summary>
        public bool IsRetirement { get; set; }
    }

    public enum RiskLevel
    {
        Low,
        Med,
        High
    }
}