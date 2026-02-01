using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartVestFinancialAdvisor.Core.Scoring
{
    /// <summary>
    /// Calculates financial scores for a client.
    /// </summary>
    public class ScoreCalculator
    {
        /// <summary>
        /// Returns detailed ScoreResult with sub-scores.
        /// </summary>
        public ScoreResult Calculate(ClientProfile client)
        {
            var subScores = BuildSubScores(client);

            ValidateWeights(subScores);

            return new ScoreResult(subScores);
        }

        /// <summary>
        /// Returns FinancialScore for Builder.
        /// </summary>
        public FinancialScore AggregateScore(ClientProfile client)
        {
            var result = Calculate(client);

            return new FinancialScore
            {
                Total = result.TotalScore,
                SubScores = result.SubScores
            };
        }

        // -------------------------
        // Build SubScores
        // -------------------------
        private List<SubScore> BuildSubScores(ClientProfile client)
        {
            return new List<SubScore>
            {
                new SubScore("Income Stability", CalculateIncomeScore(client), 0.40m),
                new SubScore("Savings Health", CalculateSavingsScore(client), 0.30m),
                new SubScore("Debt Load", CalculateDebtScore(client), 0.30m)
            };
        }

        // -------------------------
        // Individual score logic
        // -------------------------
        private decimal CalculateIncomeScore(ClientProfile client)
        {
            if (client.MonthlyIncome <= 0) return 0m;
            if (client.MonthlyIncome >= 8000) return 100m;
            return (client.MonthlyIncome / 8000m) * 100m;
        }

        private decimal CalculateSavingsScore(ClientProfile client)
        {
            if (client.Savings <= 0) return 0m;
            if (client.Savings >= 50000) return 100m;
            return (client.Savings / 50000m) * 100m;
        }

        private decimal CalculateDebtScore(ClientProfile client)
        {
            if (client.MonthlyDebt <= 0) return 100m;

            decimal debtRatio = client.MonthlyDebt / Math.Max(client.MonthlyIncome, 1);
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