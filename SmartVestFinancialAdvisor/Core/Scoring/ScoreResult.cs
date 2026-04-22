using System.Collections.Generic;
using System.Linq;

namespace SmartVestFinancialAdvisor.Core.Scoring
{
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

    public class FinancialScore
    {
        public decimal Total { get; set; }
        public IReadOnlyList<SubScore> SubScores { get; set; } = new List<SubScore>();

        public static implicit operator FinancialScore(decimal v)
        {
            throw new NotImplementedException();
        }
    }
}
