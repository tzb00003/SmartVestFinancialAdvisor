using System.Collections.Generic;
using System.Linq;

namespace SmartVestFinancialAdvisor.Core.Scoring
{
    /// <summary>
    /// Represents the detailed score calculation result.
    /// </summary>
    public class ScoreResult
    {
        /// <summary>The final weighted aggregate score (usually out of 100).</summary>
        public decimal TotalScore { get; }
        /// <summary>The list of individual factor scores that make up the total.</summary>
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
        /// <summary>The final aggregate score.</summary>
        public decimal Total { get; set; }
        /// <summary>Individual sub-score components.</summary>
        public IReadOnlyList<SubScore> SubScores { get; set; } = new List<SubScore>();
    }
}
