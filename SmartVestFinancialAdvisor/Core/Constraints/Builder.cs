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

        public async Task<BuildResult> Build(FinancialProfile profile)
        {
            // 1. Map to ClientProfile
            var client = MapToClientProfile(profile);

            // 2. Fetch Demographic Benchmark for contextual analysis
            var benchmark = await _benchmarkProvider.GetIncomeBenchmarkAsync(
                client.Age,
                client.LocationState ?? "NY",
                client.Gender
            );

            // 3. Get the Score
            var score = await _scoreCalculator.AggregateScore(client);

            // 4. Determine Category (Peer Model compares user against P25/P75)
            var category = await Categories.DetermineCategoryAsync(
                client,
                _scoreCalculator,
                _benchmarkProvider
            );

            // 5. Run the Advisor Engine for qualitative insights
            // This runs BenchmarkAgent, PortfolioAgent, and SavingsAgent in parallel
            var analysisResults = await _advisorEngine.RunAnalysisAsync(client, score, benchmark);

            // 6. Finalize Constraints based on Category Definition
            var definition = Categories.GetCategoryDefinition(category);

            var constraints = new PortfolioConstraints
            {
                RiskTolerance = profile.RiskTolerance,
                MaxStockAllocation = definition.DefaultStockAllocation,
                MaxBondAllocation = definition.DefaultBondAllocation,
                MaxCashAllocation = definition.DefaultCashAllocation
            };

            return new BuildResult
            {
                Score = score,
                Category = category,
                Constraints = constraints,
                Insights = analysisResults.ToList() // Added to result
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

        /// <summary>
        /// The prioritized collection of agent-generated recommendations.
        /// </summary>
        public List<AnalysisResult> Insights { get; set; } = new();
    }
}