using System.Linq;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Benchmarks;
using SmartVestFinancialAdvisor.Infrastructure.Benchmarks;

namespace SmartVestFinancialAdvisor.Core.Scoring
{
    /// <summary>
    /// Calculates financial scores for a client.
    /// </summary>
    public class ScoreCalculator
    {
        private readonly IBenchmarkProvider _benchmarkProvider;

        public ScoreCalculator(IBenchmarkProvider benchmarkProvider)
        {
            _benchmarkProvider = benchmarkProvider;
        }

        /// <summary>
        /// Returns detailed ScoreResult with sub-scores.
        /// </summary>
        public async Task<ScoreResult> Calculate(ClientProfile client)
        {
            var subScores = await BuildSubScores(client);

            ValidateWeights(subScores);

            return new ScoreResult(subScores);
        }

        /// <summary>
        /// Returns FinancialScore for Builder.
        /// </summary>
        public async Task<FinancialScore> AggregateScore(ClientProfile client)
        {
            var result = await Calculate(client);

            return new FinancialScore
            {
                Total = result.TotalScore,
                SubScores = result.SubScores
            };
        }

        // -------------------------
        // Build SubScores
        // -------------------------
        private async Task<List<SubScore>> BuildSubScores(ClientProfile client)
        {
            return new List<SubScore>
            {
                new SubScore("Income Stability", await CalculateIncomeScore(client), 0.40m),
                new SubScore("Savings Health", CalculateSavingsScore(client), 0.30m),
                new SubScore("Debt Load", CalculateDebtScore(client), 0.30m)
            };
        }

        // -------------------------
        // Individual score logic
        // -------------------------
        private async Task<decimal> CalculateIncomeScore(ClientProfile client)
        {
            if (client.MonthlyIncome <= 0) return 0m;

            // Fetch top-tier income ceiling for state (latest year, prefer Census)
            var ceilingAnnual = await _benchmarkProvider.GetTopTierIncomeCeilingAsync(
                client.Age,
                client.LocationState ?? "NY",
                client.Gender
            );

            // Use ceiling if available, otherwise fallback to hardcoded annual value
            decimal benchmarkThreshold = (ceilingAnnual ?? 96000m) / 12m;

            if (client.MonthlyIncome >= benchmarkThreshold) return 100m;
            return (client.MonthlyIncome / benchmarkThreshold) * 100m;
        }

        private decimal CalculateSavingsScore(ClientProfile client)
        {
            if (client.Savings <= 0) return 0m;
            if (client.Savings >= 50000) return 100m;
            return (client.Savings / 50000m) * 100m;
        }

        private decimal CalculateDebtScore(ClientProfile client)
        {
            if (client.MonthlyExpense <= 0) return 100m;

            decimal debtRatio = client.MonthlyExpense / Math.Max(client.MonthlyIncome, 1);
            if (debtRatio >= 0.6m) return 0m;
            return (1 - debtRatio) * 100m;
        }


        // -------------------------
        // Validate weights sum to 1
        // -------------------------
        private void ValidateWeights(IEnumerable<SubScore> subScores)
        {
            decimal totalWeight = subScores.Sum(s => s.Weight);
            if (totalWeight != 1.0m)
                throw new InvalidOperationException($"Score weights must total 1.0. Current total: {totalWeight}");
        }
    }
}
