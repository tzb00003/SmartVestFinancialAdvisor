using SmartVestFinancialAdvisor.Components.Models;
using SmartVestFinancialAdvisor.Core.Constraints;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartVestFinancialAdvisor.Components.Services
{
    public interface IFinancialSurveyService
    {
        Task<BuildResult> SaveAsync(FinancialSurveyModel model, CancellationToken ct);
    }

    /// <summary>
    /// Maps the UI model to the Core profile and runs the Advisor Engine.
    /// </summary>
    public sealed class FinancialSurveyService : IFinancialSurveyService
    {
        private readonly Builder _builder;

        public FinancialSurveyService(Builder builder)
        {
            _builder = builder;
        }

        public async Task<BuildResult> SaveAsync(FinancialSurveyModel model, CancellationToken ct)
        {
            // Map the UI RiskLevel to a decimal (0.0 to 1.0)
            decimal mappedRisk = model.RiskLevel switch
            {
                RiskLevel.Low => 0.25m,
                RiskLevel.Med => 0.50m,
                RiskLevel.High => 0.75m,
                _ => 0.25m
            };

            // Map UI items to Core items
            var mappedItems = model.Items.Select(i => new Core.Financial.FinancialItem
            {
                Label = i.Label ?? "Unnamed Item",
                Amount = i.Amount ?? 0m,
                MonthlyPayment = i.MonthlyPayment ?? 0m,
                InterestRate = i.InterestRate ?? 0m, // Ensure decimal fraction
                IsDebt = i.IsDebt,
                IsRetirement = i.IsRetirement
            }).ToList();

            // Create the Core Profile
            var profile = new FinancialProfile
            {
                MonthlyIncome = model.MonthlyIncome ?? 0m,
                Savings = model.Savings ?? 0m,
                Debt = model.Debt ?? 0m,
                MonthlyExpense = model.MonthlyExpense ?? 0m,
                Age = model.Age ?? 0,
                LocationState = model.State,
                RiskTolerance = mappedRisk,
                Items = mappedItems
            };

            // Execute the Builder and return the result
            return await _builder.Build(profile);
        }
    }
}