using System;
using SmartVestFinancialAdvisor.Core.Benchmarks;

namespace SmartVestFinancialAdvisor.Core.Scoring
{
    public enum ClientCategory
    {
        Conservative,
        Balanced,
        Aggressive
    }

    public class CategoryDefinition
    {
        public decimal DefaultStockAllocation { get; set; }
        public decimal DefaultBondAllocation { get; set; }
        public decimal DefaultCashAllocation { get; set; }
    }

    public static class Categories
    {
        public static ClientCategory DetermineCategory(FinancialScore score, ClientProfile profile, IncomeBenchmark? benchmark = null)
        {
            // 1. Determine base category from raw score
            ClientCategory baseCategory;
            if (score.Total < 40) baseCategory = ClientCategory.Conservative;
            else if (score.Total < 70) baseCategory = ClientCategory.Balanced;
            else baseCategory = ClientCategory.Aggressive;

            // 2. Adjust based on benchmark (if available)
            if (benchmark != null)
            {
                decimal annualIncome = profile.MonthlyIncome * 12;

                // If client income is > 20% above median, they might have higher capacity for risk
                if (annualIncome > benchmark.MedianIncome * 1.2m)
                {
                    // Upgrade category if possible (Conservative -> Balanced -> Aggressive)
                    if (baseCategory == ClientCategory.Conservative) return ClientCategory.Balanced;
                    if (baseCategory == ClientCategory.Balanced) return ClientCategory.Aggressive;
                }

                // If client income is < 20% below median, they might need more stability
                if (annualIncome < benchmark.MedianIncome * 0.8m)
                {
                    // Downgrade category if possible
                    if (baseCategory == ClientCategory.Aggressive) return ClientCategory.Balanced;
                    if (baseCategory == ClientCategory.Balanced) return ClientCategory.Conservative;
                }
            }

            return baseCategory;
        }

        public static CategoryDefinition GetCategoryDefinition(ClientCategory category)
        {
            return category switch
            {
                ClientCategory.Conservative => new CategoryDefinition { DefaultStockAllocation = 0.3m, DefaultBondAllocation = 0.6m, DefaultCashAllocation = 0.1m },
                ClientCategory.Balanced => new CategoryDefinition { DefaultStockAllocation = 0.5m, DefaultBondAllocation = 0.4m, DefaultCashAllocation = 0.1m },
                ClientCategory.Aggressive => new CategoryDefinition { DefaultStockAllocation = 0.7m, DefaultBondAllocation = 0.2m, DefaultCashAllocation = 0.1m },
                _ => throw new ArgumentException("Invalid category")
            };
        }
    }
}