using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Components.Models;
using SmartVestFinancialAdvisor.Components.Services; // <-- IMPORTANT: update if your interface lives elsewhere
using SmartVestFinancialAdvisor.Core.Agents;
using SmartVestFinancialAdvisor.Core.Benchmarks;
using SmartVestFinancialAdvisor.Core.Financial;
using SmartVestFinancialAdvisor.Core.Scoring;

namespace SmartVestFinancialAdvisor.Core.Constraints
{
    public class Builder
    {
        private readonly ScoreCalculator _scoreCalculator;
        private readonly IBenchmarkProvider _benchmarkProvider;
        private readonly AdvisorEngine _advisorEngine;
        private readonly IRecommendationCatalog _recommendationCatalog;

        public Builder(
            IBenchmarkProvider benchmarkProvider,
            ScoreCalculator scoreCalculator,
            AdvisorEngine advisorEngine,
            IRecommendationCatalog recommendationCatalog)
        {
            _benchmarkProvider = benchmarkProvider;
            _scoreCalculator = scoreCalculator;
            _advisorEngine = advisorEngine;
            _recommendationCatalog = recommendationCatalog;
        }

        public async Task<BuildResult> Build(FinancialProfile profile)
        {
            var client = MapToClientProfile(profile);

            var benchmark = await _benchmarkProvider.GetIncomeBenchmarkAsync(
                client.Age,
                client.LocationState ?? "NY",
                client.Gender
            );

            var score = await _scoreCalculator.AggregateScore(client);

            var category = await Categories.DetermineCategoryAsync(
                client,
                _scoreCalculator,
                _benchmarkProvider
            );

            var analysisResults = await _advisorEngine.RunAnalysisAsync(client, score, benchmark);

            var definition = Categories.GetCategoryDefinition(category);
            var constraints = new PortfolioConstraints
            {
                RiskTolerance = profile.RiskTolerance,
                MaxStockAllocation = definition.DefaultStockAllocation,
                MaxBondAllocation = definition.DefaultBondAllocation,
                MaxCashAllocation = definition.DefaultCashAllocation
            };

            var facts = ExtractFacts(client);

            // Build the result first (so Recommendations logic can use FinancialScore/Constraints/Facts)
            var result = new BuildResult
            {
                Score = score.Total,
                FinancialScore = score,
                Category = category,
                Constraints = constraints,
                Insights = analysisResults.ToList(),
                Facts = facts,
                Recommendations = new List<Recommendation>(),
                ComputedAt = DateTime.UtcNow
            };

            // ✅ Populate recommendations using your catalog (this is what fills Info/Score10/etc.)
            result.Recommendations = _recommendationCatalog.For(result).ToList();

            return result;
        }

        private ClientProfile MapToClientProfile(FinancialProfile profile)
        {
            return new ClientProfile
            {
                MonthlyIncome = profile.MonthlyIncome,
                Savings = profile.Savings,
                Debt = profile.Debt,
                MonthlyExpense = profile.MonthlyExpense,
                Age = profile.Age,
                LocationState = profile.LocationState,
                Gender = profile.Gender,
                Items = profile.Items ?? new List<Core.Financial.FinancialItem>()
            };
        }

        private IDictionary<string, object?> ExtractFacts(ClientProfile client)
        {
            var items = client.Items ?? new List<Core.Financial.FinancialItem>();
            decimal totalAssets = items.Where(i => !i.IsDebt).Sum(i => i.Amount);
            decimal totalLiquidAssets = items.Where(i => !i.IsDebt && !i.IsRetirement).Sum(i => i.Amount);
            decimal totalRetirementSavings = items.Where(i => !i.IsDebt && i.IsRetirement).Sum(i => i.Amount);

            decimal totalDebtBalance = items.Where(i => i.IsDebt).Sum(i => i.Amount);
            decimal totalMonthlyDebtPayments = items.Where(i => i.IsDebt).Sum(i => i.MonthlyPayment);

            decimal weightedDebtRate = 0m;
            if (totalDebtBalance > 0m)
            {
                weightedDebtRate = items
                    .Where(i => i.IsDebt)
                    .Sum(i => i.InterestRate * (i.Amount / totalDebtBalance));
            }

            if (totalLiquidAssets <= 0m && client.Savings > 0m)
                totalLiquidAssets = client.Savings;

            if (totalMonthlyDebtPayments <= 0m && client.Debt > 0m)
                totalMonthlyDebtPayments = client.Debt;

            decimal monthlyExpenses = client.MonthlyExpense > 0 ? client.MonthlyExpense : (client.MonthlyIncome * 0.7m);
            decimal emergencyMonths = monthlyExpenses > 0m ? totalLiquidAssets / monthlyExpenses : 0m;

            decimal averageDebtAprPercent = weightedDebtRate > 0m ? weightedDebtRate * 100m : 7.0m;

            var facts = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["MonthlyIncome"] = client.MonthlyIncome,
                ["MonthlyExpenses"] = monthlyExpenses,
                ["MonthlyExpense"] = monthlyExpenses,

                ["TotalAssets"] = totalAssets,
                ["TotalLiquidAssets"] = totalLiquidAssets,
                ["TotalRetirementSavings"] = totalRetirementSavings,
                ["Cash"] = totalLiquidAssets,

                ["TotalDebtBalance"] = totalDebtBalance,
                ["TotalMonthlyDebtPayments"] = totalMonthlyDebtPayments,
                ["WeightedDebtRate"] = weightedDebtRate,
                ["AverageDebtAPR"] = averageDebtAprPercent,

                ["EmergencyMonths"] = emergencyMonths,

                ["HasEmployerMatch"] = false,
                ["IsHighTaxBracket"] = false,
                ["InflationConcern"] = false
            };

            return facts;
        }
    }

    public class BuildResult
    {
        [JsonPropertyName("score")]
        public decimal Score { get; set; }

        [JsonPropertyName("financialScore")]
        public FinancialScore? FinancialScore { get; set; }

        [JsonPropertyName("category")]
        public ClientCategory Category { get; set; }

        [JsonPropertyName("constraints")]
        public PortfolioConstraints? Constraints { get; set; }

        [JsonPropertyName("facts")]
        public IDictionary<string, object?>? Facts { get; set; }

        [JsonPropertyName("insights")]
        public List<AnalysisResult>? Insights { get; set; }

        [JsonPropertyName("recommendations")]
        public List<Recommendation>? Recommendations { get; set; } = new();

        [JsonPropertyName("computedAt")]
        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
    }
}