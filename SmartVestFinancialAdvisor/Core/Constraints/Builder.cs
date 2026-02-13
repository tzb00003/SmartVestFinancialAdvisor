using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Benchmarks;
using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Financial;
using SmartVestFinancialAdvisor.Core.Constraints;

namespace SmartVestFinancialAdvisor.Core.Constraints
{
    public class Builder
    {
        private readonly ScoreCalculator _scoreCalculator;
        private readonly IBenchmarkProvider _benchmarkProvider;

        // Better: Inject the calculator. This makes testing much easier!
        public Builder(IBenchmarkProvider benchmarkProvider, ScoreCalculator scoreCalculator)
        {
            _benchmarkProvider = benchmarkProvider;
            _scoreCalculator = scoreCalculator;
        }

        public async Task<BuildResult> Build(FinancialProfile profile)
        {
            // 1. Map to ClientProfile
            var client = MapToClientProfile(profile);

            // 2. Get the Score (Do this first so we have the data ready)
            var score = await _scoreCalculator.AggregateScore(client);

            // 3. Determine Category (Peer Model)
            var category = await Categories.DetermineCategoryAsync(
                client,
                _scoreCalculator,
                _benchmarkProvider
            );

            // 4. Get Rules & Finalize Constraints
            var definition = Categories.GetCategoryDefinition(category);

            var constraints = new PortfolioConstraints
            {
                // We use the category defaults, but you could "nudge" them 
                // based on the profile.RiskTolerance here if you wanted.
                RiskTolerance = profile.RiskTolerance,
                MaxStockAllocation = definition.DefaultStockAllocation,
                MaxBondAllocation = definition.DefaultBondAllocation,
                MaxCashAllocation = definition.DefaultCashAllocation
            };

            return new BuildResult
            {
                Score = score,
                Category = category,
                Constraints = constraints
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
}

/// <summary>
/// Container for the objects produced during the build process.
/// </summary>
public class BuildResult
{
    /// <summary>The calculated multidimensional financial score.</summary>
    public FinancialScore Score { get; set; } = null!;

    /// <summary>The risk category assigned to the client.</summary>
    public ClientCategory Category { get; set; }

    /// <summary>The target portfolio allocation limits.</summary>
    public PortfolioConstraints Constraints { get; set; } = null!;
}