using System;

namespace SmartVestFinancialAdvisor.Core.Scoring
{
    public enum ClientCategory
    {
        Conservative,
        Balanced,
        Aggressive
    }

    public static class Categories
    {
        public static ClientCategory DetermineCategory(FinancialScore score)
        {
            if (score.Total < 40) return ClientCategory.Conservative;
            if (score.Total < 70) return ClientCategory.Balanced;
            return ClientCategory.Aggressive;
        }

        public static dynamic GetCategoryDefinition(ClientCategory category)
        {
            return category switch
            {
                ClientCategory.Conservative => new { DefaultStockAllocation = 0.3m, DefaultBondAllocation = 0.6m, DefaultCashAllocation = 0.1m },
                ClientCategory.Balanced => new { DefaultStockAllocation = 0.5m, DefaultBondAllocation = 0.4m, DefaultCashAllocation = 0.1m },
                ClientCategory.Aggressive => new { DefaultStockAllocation = 0.7m, DefaultBondAllocation = 0.2m, DefaultCashAllocation = 0.1m },
                _ => throw new ArgumentException("Invalid category")
            };
        }
    }
}