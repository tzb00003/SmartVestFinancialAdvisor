using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public Builder(
            IBenchmarkProvider benchmarkProvider,
            ScoreCalculator scoreCalculator,
            AdvisorEngine advisorEngine)
        {
            _benchmarkProvider = benchmarkProvider;
            _scoreCalculator = scoreCalculator;
            _advisorEngine = advisorEngine;
        }

        public async Task<BuildResult> Build(FinancialProfile profile)
        {
            // 1. Map to ClientProfile
            var client = MapToClientProfile(profile);

            // 2. Fetch benchmark
            var benchmark = await _benchmarkProvider.GetIncomeBenchmarkAsync(
                client.Age,
                client.LocationState ?? "NY",
                client.Gender
            );

            // 3. Get the Score
            var score = await _scoreCalculator.AggregateScore(client);

            // 4. Determine Category
            var category = await Categories.DetermineCategoryAsync(
                client,
                _scoreCalculator,
                _benchmarkProvider
            );

            // 5. Run the Advisor Engine
            var analysisResults = await _advisorEngine.RunAnalysisAsync(client, score, benchmark);

            // 6. Finalize Constraints
            var definition = Categories.GetCategoryDefinition(category);
            var constraints = new PortfolioConstraints
            {
                RiskTolerance = profile.RiskTolerance,
                MaxStockAllocation = definition.DefaultStockAllocation,
                MaxBondAllocation = definition.DefaultBondAllocation,
                MaxCashAllocation = definition.DefaultCashAllocation
            };

            // -------- FACTS (for recommender + UI) ----------
            var items = client.Items ?? new List<FinancialItem>();

            decimal totalAssets = items.Where(i => !i.IsDebt).Sum(i => i.Amount);
            decimal totalLiquidAssets = items.Where(i => !i.IsDebt && !i.IsRetirement).Sum(i => i.Amount);
            decimal totalRetirementSavings = items.Where(i => !i.IsDebt && i.IsRetirement).Sum(i => i.Amount);

            decimal totalDebtBalance = items.Where(i => i.IsDebt).Sum(i => i.Amount);
            decimal totalMonthlyDebtPayments = items.Where(i => i.IsDebt).Sum(i => i.MonthlyPayment);

            decimal weightedDebtRate = 0m; // FRACTION (0.07 == 7%)
            if (totalDebtBalance > 0m)
            {
                weightedDebtRate = items
                    .Where(i => i.IsDebt)
                    .Sum(i => i.InterestRate * (i.Amount / totalDebtBalance));
            }

            // Fallbacks if items are missing
            if (totalLiquidAssets <= 0m && client.Savings > 0m) totalLiquidAssets = client.Savings;
            if (totalMonthlyDebtPayments <= 0m && client.Debt > 0m) totalMonthlyDebtPayments = client.Debt;

            decimal monthlyExpenses = client.MonthlyExpense;
            decimal emergencyMonths = monthlyExpenses > 0m ? totalLiquidAssets / monthlyExpenses : 0m;

            // RecommendationCatalog historically expects APR as percent number (7 == 7%)
            decimal avgAprPercent = weightedDebtRate > 0m ? weightedDebtRate * 100m : 7.0m;

            bool hasEmployerMatch = false;
            bool highTaxBracket = false;
            bool inflationConcern = false;

            var facts = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["MonthlyIncome"] = client.MonthlyIncome,
                ["MonthlyExpenses"] = monthlyExpenses,

                ["TotalAssets"] = totalAssets,
                ["TotalLiquidAssets"] = totalLiquidAssets,
                ["TotalRetirementSavings"] = totalRetirementSavings,

                ["TotalDebtBalance"] = totalDebtBalance,
                ["TotalMonthlyDebtPayments"] = totalMonthlyDebtPayments,

                // Both units provided:
                ["WeightedDebtRate"] = weightedDebtRate,         // fraction (0.07 = 7%)
                ["AverageDebtAPR"] = avgAprPercent,              // percent number (7 = 7%)

                ["Cash"] = totalLiquidAssets,
                ["EmergencyMonths"] = emergencyMonths,

                ["HasEmployerMatch"] = hasEmployerMatch,
                ["IsHighTaxBracket"] = highTaxBracket,
                ["InflationConcern"] = inflationConcern
            };
            // -----------------------------------------------

            return new BuildResult
            {
                Score = score,
                Category = category,
                Constraints = constraints,
                Insights = analysisResults.ToList(),
                Facts = facts
            };
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
                Items = profile.Items
            };
        }
    }

    public class BuildResult
    {
        public FinancialScore Score { get; set; } = null!;
        public ClientCategory Category { get; set; }
        public PortfolioConstraints Constraints { get; set; } = null!;
        public List<AnalysisResult> Insights { get; set; } = new();

        public IDictionary<string, object?> Facts { get; set; }
            = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }
}