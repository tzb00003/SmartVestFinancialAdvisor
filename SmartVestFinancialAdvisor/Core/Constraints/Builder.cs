using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Benchmarks;
using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Financial;
using SmartVestFinancialAdvisor.Core.Agents; // Added for AdvisorEngine

namespace SmartVestFinancialAdvisor.Core.Constraints
{
    public class Builder
    {
        private readonly ScoreCalculator _scoreCalculator;
        private readonly IBenchmarkProvider _benchmarkProvider;
        private readonly AdvisorEngine _advisorEngine; // Added orchestrator

        public Builder(
            IBenchmarkProvider benchmarkProvider,
            ScoreCalculator scoreCalculator,
            AdvisorEngine advisorEngine) // Injected
        {
            _benchmarkProvider = benchmarkProvider;
            _scoreCalculator = scoreCalculator;
            _advisorEngine = advisorEngine;
        }
        /*
         * Add a Getscore function
         * 
         */
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

            // -------- FACTS (for the recommender) ----------
            // Adjust these if your "Savings" is not liquid cash.
            decimal monthlyExpenses = client.MonthlyExpense;
            decimal cashLike = client.Savings; // <-- if this is "cash". If it's total assets, switch to your cash property.
            decimal emergencyMonths = monthlyExpenses > 0 ? cashLike / monthlyExpenses : 0m;

            // If you track APRs elsewhere, replace with your real average.
            // You can also compute a weighted APR from client.Items if you have balances.
            decimal avgApr = 7.0m; // sensible default

            // If you capture these in the survey, set them here; otherwise leave defaults.
            bool hasEmployerMatch = false;
            bool highTaxBracket = false;
            bool inflationConcern = false;

            var facts = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["MonthlyExpenses"] = monthlyExpenses,
                ["Cash"] = cashLike,
                ["EmergencyMonths"] = emergencyMonths,
                ["AverageDebtAPR"] = avgApr,
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

    /// <summary>
    /// Container for the objects produced during the build process.
    /// </summary>
    public class BuildResult
    {
        public FinancialScore Score { get; set; } = null!;
        public ClientCategory Category { get; set; }
        public PortfolioConstraints Constraints { get; set; } = null!;
        public List<AnalysisResult> Insights { get; set; } = new();

        // NEW: lightweight context for the recommender
        public IDictionary<string, object?> Facts { get; set; }
            = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }
}