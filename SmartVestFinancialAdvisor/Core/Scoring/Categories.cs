using System;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Benchmarks;

namespace SmartVestFinancialAdvisor.Core.Scoring
{
    public enum ClientCategory
    {
        Conservative, // Bottom 25% - Needs safety
        Balanced,     // Mid-range (P25 to P75)
        Aggressive    // Top 25% (Above P75) - Can handle growth risk
    }

    public class CategoryDefinition
    {
        public decimal DefaultStockAllocation { get; set; }
        public decimal DefaultBondAllocation { get; set; }
        public decimal DefaultCashAllocation { get; set; }
    }

    public static class Categories
    {
        /// <summary>
        /// Classifies the client by comparing their total financial health score 
        /// against "Gatekeeper" peer avatars (P25 and P75).
        /// </summary>
        public static async Task<ClientCategory> DetermineCategoryAsync(
            ClientProfile userProfile,
            ScoreCalculator scoreCalculator,
            IBenchmarkProvider benchmarkProvider)
        {
            // 1. Fetch benchmark using the simplified single-age parameter
            var benchmark = await benchmarkProvider.GetIncomeBenchmarkAsync(
                userProfile.Age,
                userProfile.LocationState ?? "NY",
                userProfile.Gender
            );

            // Fallback: If no benchmark data is available, use static score brackets
            if (benchmark == null)
            {
                var userScore = await scoreCalculator.AggregateScore(userProfile);
                return userScore.Total switch
                {
                    < 40 => ClientCategory.Conservative,
                    < 75 => ClientCategory.Balanced,
                    _ => ClientCategory.Aggressive
                };
            }

            // 2. Generate the "Gatekeeper" Peers
            // These represent the 'low bar' and 'high bar' for the user's demographic
            var p25Peer = scoreCalculator.CreatePeerAvatar(benchmark, "P25");
            var p75Peer = scoreCalculator.CreatePeerAvatar(benchmark, "P75");

            // 3. Calculate Scores for comparison
            var userScoreResult = await scoreCalculator.AggregateScore(userProfile);
            var p25ScoreResult = await scoreCalculator.AggregateScore(p25Peer);
            var p75ScoreResult = await scoreCalculator.AggregateScore(p75Peer);

            // 4. Bracket Comparison Logic
            // If user score < P25 peer score -> Conservative
            if (userScoreResult.Total < p25ScoreResult.Total)
            {
                return ClientCategory.Conservative;
            }

            // If user score < P75 peer score -> Balanced
            if (userScoreResult.Total < p75ScoreResult.Total)
            {
                return ClientCategory.Balanced;
            }

            // User is in the top 25% of their regional demographic
            return ClientCategory.Aggressive;
        }

        public static CategoryDefinition GetCategoryDefinition(ClientCategory category)
        {
            return category switch
            {
                ClientCategory.Conservative => new CategoryDefinition
                {
                    DefaultStockAllocation = 0.3m,
                    DefaultBondAllocation = 0.6m,
                    DefaultCashAllocation = 0.1m
                },
                ClientCategory.Balanced => new CategoryDefinition
                {
                    DefaultStockAllocation = 0.5m,
                    DefaultBondAllocation = 0.4m,
                    DefaultCashAllocation = 0.1m
                },
                ClientCategory.Aggressive => new CategoryDefinition
                {
                    DefaultStockAllocation = 0.8m, // Increased for Aggressive
                    DefaultBondAllocation = 0.15m,
                    DefaultCashAllocation = 0.05m
                },
                _ => throw new ArgumentException("Invalid category")
            };
        }
    }
}