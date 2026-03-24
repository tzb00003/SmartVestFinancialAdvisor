using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Benchmarks;
using SmartVestFinancialAdvisor.Core.Financial;
using SmartVestFinancialAdvisor.Core.Scoring;

namespace SmartVestFinancialAdvisor.Core.Agents
{
    public class PortfolioAgent : IAdvisorAgent
    {
        public string Name => "Portfolio Optimization Agent";

        public Task<IEnumerable<AnalysisResult>> AnalyzeAsync(ClientProfile profile, FinancialScore score, IncomeBenchmark? benchmark)
        {
            var results = new List<AnalysisResult>();
            var items = profile.Items ?? new List<FinancialItem>();

            var debts = items.Where(i => i.IsDebt).ToList();
            var assets = items.Where(i => !i.IsDebt).ToList();

            // 1. Toxic Debt Detection (Threshold: > 7%)
            foreach (var debt in debts.Where(d => d.InterestRate > 0.07m))
            {
                results.Add(new AnalysisResult(
                    Name,
                    $"Toxic Interest: {debt.Label} ({debt.InterestRate:P1}).",
                    "This debt costs more than the average stock market return. Cease new investments and pay this off aggressively.",
                    ImpactLevel.Critical));
            }

            // 2. Negative Arbitrage Check
            if (assets.Any() && debts.Any())
            {
                var maxDebtRate = debts.Max(d => d.InterestRate);
                var savingsRate = 0.04m; // Assumed HYSA yield, could be dynamic

                if (maxDebtRate > savingsRate + 0.02m) // 2% spread threshold
                {
                    results.Add(new AnalysisResult(
                        Name,
                        "Value Leak (Negative Arbitrage).",
                        "You are holding cash/assets earning low returns while paying high interest on debt. Using excess cash to pay down this debt is a 'guaranteed' return.",
                        ImpactLevel.Warning));
                }
            }

            return Task.FromResult<IEnumerable<AnalysisResult>>(results);
        }
    }
}
