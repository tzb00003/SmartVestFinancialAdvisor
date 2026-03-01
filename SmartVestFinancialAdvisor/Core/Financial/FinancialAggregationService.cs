using System.Collections.Generic;
using System.Linq;

namespace SmartVestFinancialAdvisor.Core.Financial
{
    public class FinancialAggregationService
    {
        public AggregatedFinancials Aggregate(IEnumerable<FinancialItem> items)
        {
            var list = items?.ToList() ?? new List<FinancialItem>();

            // --- Asset Calculations ---
            var totalAssets = list.Where(i => !i.IsDebt).Sum(i => i.Amount);

            // NEW: Split assets into Liquid vs Retirement
            var totalLiquidAssets = list.Where(i => !i.IsDebt && !i.IsRetirement).Sum(i => i.Amount);
            var totalRetirementSavings = list.Where(i => !i.IsDebt && i.IsRetirement).Sum(i => i.Amount);

            // --- Debt Calculations ---
            var totalDebt = list.Where(i => i.IsDebt).Sum(i => i.Amount);
            var totalMonthlyDebtPayments = list.Where(i => i.IsDebt).Sum(i => i.MonthlyPayment);

            decimal weightedDebtRate = 0m;
            if (totalDebt > 0)
            {
                weightedDebtRate = list
                    .Where(i => i.IsDebt)
                    .Sum(i => i.InterestRate * (i.Amount / totalDebt));
            }

            return new AggregatedFinancials(
                totalAssets,
                totalLiquidAssets,
                totalRetirementSavings,
                totalDebt,
                weightedDebtRate,
                totalMonthlyDebtPayments);
        }
    }

    public record AggregatedFinancials(
        decimal TotalAssets,
        decimal TotalLiquidAssets,
        decimal TotalRetirementSavings,
        decimal TotalDebt,
        decimal WeightedDebtRate,
        decimal TotalMonthlyDebtPayments
    );
}