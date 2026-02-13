using System.ComponentModel.DataAnnotations;

namespace SmartVestFinancialAdvisor.Components.Models
{
    public sealed class FinancialSurveyModel
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name must be 100 characters or fewer")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Age is required")]
        [Range(18, 120, ErrorMessage = "Age must be between 18 and 120")]
        public int? Age { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        public string? Gender { get; set; }

        [Required(ErrorMessage = "Annual income is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Annual income must be ≥ 0")]
        public decimal? AnnualIncome { get; set; }

        [Required(ErrorMessage = "Total debt is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Total debt must be ≥ 0")]
        public decimal? TotalDebt { get; set; }

        [Required(ErrorMessage = "Monthly debt expenses are required")]
        [Range(0, double.MaxValue, ErrorMessage = "Monthly debt expenses must be ≥ 0")]
        public decimal? MonthlyDebtExpenses { get; set; }

        [Required(ErrorMessage = "Monthly expenses are required")]
        [Range(0, double.MaxValue, ErrorMessage = "Monthly expenses must be ≥ 0")]
        public decimal? MonthlyExpenses { get; set; }

        [Required(ErrorMessage = "Credit score is required")]
        [Range(300, 850, ErrorMessage = "Credit score must be between 300 and 850")]
        public int? CreditScore { get; set; }
    }
}
