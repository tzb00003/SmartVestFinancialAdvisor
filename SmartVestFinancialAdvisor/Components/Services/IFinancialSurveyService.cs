using SmartVestFinancialAdvisor.Components.Models;
using SmartVestFinancialAdvisor.Core.Constraints;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartVestFinancialAdvisor.Components.Services
{
    public interface IFinancialSurveyService
    {
        Task<BuildResult?> AnalyzeAsync(FinancialSurveyModel decryptedSurvey);
        Task<BuildResult> SaveAsync(FinancialSurveyModel model, CancellationToken ct);
    }

    public sealed class FinancialSurveyService : IFinancialSurveyService
    {
        private readonly Builder _builder;

        public FinancialSurveyService(Builder builder)
        {
            _builder = builder;
        }

        public async Task<BuildResult?> AnalyzeAsync(FinancialSurveyModel decryptedSurvey)
        {
            return await SaveAsync(decryptedSurvey, CancellationToken.None);
        }

        public async Task<BuildResult> SaveAsync(FinancialSurveyModel model, CancellationToken ct)
        {
            decimal mappedRisk = model.RiskLevel switch
            {
                RiskLevel.Low => 0.25m,
                RiskLevel.Med => 0.50m,
                RiskLevel.High => 0.75m,
                _ => 0.25m
            };

            var mappedItems = model.Items.Select(i => new Core.Financial.FinancialItem
            {
                Label = i.Label ?? "Unnamed Item",
                Amount = i.Amount ?? 0m,
                MonthlyPayment = i.MonthlyPayment ?? 0m,
                InterestRate = i.InterestRate ?? 0m, 
                IsDebt = i.IsDebt,
                IsRetirement = i.IsRetirement
            }).ToList();

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

            return await _builder.Build(profile);
        }
    }
}