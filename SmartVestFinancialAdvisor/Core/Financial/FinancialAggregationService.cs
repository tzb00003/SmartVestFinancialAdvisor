using System.Collections.Generic;
using System.Linq;

namespace SmartVestFinancialAdvisor.Core.Financial
{
    /// <summary>
    /// Aggregates financial data from multiple sources.
    /// </summary>
    public class FinancialAggregationService
    {
        public AggregatedFinancials Aggregate(IEnumerable<FinancialItem> items)
        {
            var list = items?.ToList() ?? new List<FinancialItem>();

            var totalAssets = list.Where(i => !i.IsDebt).Sum(i => i.Amount);
            var totalDebt = list.Where(i => i.IsDebt).Sum(i => i.Amount);

            decimal weightedDebtRate = 0m;
            if (totalDebt > 0)
            {
                weightedDebtRate = list
                    .Where(i => i.IsDebt)
                    .Sum(i => i.InterestRate * (i.Amount / totalDebt));
            }

            return new AggregatedFinancials(totalAssets, totalDebt, weightedDebtRate);
        }
    }

    public record AggregatedFinancials(decimal TotalAssets, decimal TotalDebt, decimal WeightedDebtRate);
}
