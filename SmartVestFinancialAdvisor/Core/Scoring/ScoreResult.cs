using System.Collections.Generic;
using System.Linq;

namespace SmartVestFinancialAdvisor.Core.Scoring
{
    /// <summary>
    /// Represents the detailed score calculation result.
    /// </summary>
    public class ScoreResult
    {
        public decimal TotalScore { get; }
        public IReadOnlyList<SubScore> SubScores { get; }

        public ScoreResult(IEnumerable<SubScore> subScores)
        {
            SubScores = subScores.ToList();
            TotalScore = SubScores.Sum(s => s.WeightedScore);
        }
    }

    /// <summary>
    /// Represents a simplified financial score for Builder usage.
    /// </summary>
    public class FinancialScore
    {
        public decimal Total { get; set; }
        public IReadOnlyList<SubScore> SubScores { get; set; } = new List<SubScore>();
    }
}
