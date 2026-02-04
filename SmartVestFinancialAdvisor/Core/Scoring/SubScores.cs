using System;

namespace SmartVestFinancialAdvisor.Core.Scoring
{
    /// <summary>
    /// Represents an individual sub-score with a weight and name.
    /// </summary>
    public class SubScore
    {
        public string Name { get; }
        public decimal RawScore { get; }
        public decimal Weight { get; }
        public decimal WeightedScore => RawScore * Weight;

        public SubScore(string name, decimal rawScore, decimal weight)
        {
            Name = name;
            RawScore = rawScore;
            Weight = weight;
        }
    }
}