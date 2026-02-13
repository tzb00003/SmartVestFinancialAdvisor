using System;

namespace SmartVestFinancialAdvisor.Core.Scoring
{
    /// <summary>
    /// Represents a score for a specific financial dimension (e.g., "Savings Health") 
    /// within the overall evaluation framework.
    /// </summary>
    public class SubScore
    {
        /// <summary>Human-readable name of the scoring factor.</summary>
        public string Name { get; }

        /// <summary>The original score assigned to this factor before weighting (usually 0-100).</summary>
        public decimal RawScore { get; }

        /// <summary>The weight percentage assigned to this factor (e.g., 0.30 for 30%).</summary>
        public decimal Weight { get; }

        /// <summary>The final calculated score contribution (<see cref="RawScore"/> * <see cref="Weight"/>).</summary>
        public decimal WeightedScore => RawScore * Weight;

        /// <summary>
        /// Initializes a new instance of a SubScore.
        /// </summary>
        public SubScore(string name, decimal rawScore, decimal weight)
        {
            Name = name;
            RawScore = rawScore;
            Weight = weight;
        }
    }
}