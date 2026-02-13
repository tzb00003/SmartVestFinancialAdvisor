using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Benchmarks;
using SmartVestFinancialAdvisor.Core.Financial;
using SmartVestFinancialAdvisor.Core.Scoring;

namespace SmartVestFinancialAdvisor.Core.Agents
{
    /// <summary>
    /// An advisor agent focused on evaluating the client's asset and debt structure 
    /// and identifying optimization opportunities.
    /// </summary>
    public class PortfolioAgent : IAdvisorAgent
    {
        /// <inheritdoc/>
        public string Name => "Portfolio Agent";

        /// <summary>
        /// Analyzes the specific composition of assets and debts to find risks like toxic interest rates.
        /// </summary>
        public Task<IEnumerable<AnalysisResult>> AnalyzeAsync(ClientProfile profile, FinancialScore score, IncomeBenchmark? benchmark)
        {
            var results = new List<AnalysisResult>();
            var items = profile.Items ?? new List<FinancialItem>();

            if (items.Count == 0)
            {
                return Task.FromResult<IEnumerable<AnalysisResult>>(results);
            }

            var debts = items.Where(i => i.IsDebt).ToList();
            var assets = items.Where(i => !i.IsDebt).ToList();

            // 1. Toxic Debt Detection (> 7% is typically considered high interest and is high usual stock market returns)
            var toxicDebt = debts
                .Where(d => d.InterestRate > 7m)
                .OrderByDescending(d => d.InterestRate)
                .ToList();

            foreach (var debt in toxicDebt)
            {
                results.Add(new AnalysisResult(
                    Name,
                    $"Toxic debt detected: {debt.Label} at {debt.InterestRate}%.",
                    $"URGENT: Prioritize paying off {debt.Label}. Its {debt.InterestRate}% interest rate is likely outstripping investment returns.",
                    ImpactLevel.Critical
                ));
            }

            // 2. Emergency Fund Verification
            if (!assets.Any(a => a.Label.Trim().Equals("Emergency Fund", System.StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(new AnalysisResult(
                    Name,
                    "No designated emergency fund found.",
                    "ADVICE: You lack a designated Emergency Fund in your tracked assets. Ensure you have at least 3-6 months of expenses in a liquid account.",
                    ImpactLevel.Warning
                ));
            }

            // 3. Negative Arbitrage Check (Paying more in debt than earning on assets)
            if (assets.Count > 0 && debts.Count > 0)
            {
                var maxDebtRate = debts.Max(d => d.InterestRate);
                var minAssetRate = assets.Min(a => a.InterestRate);

                if (maxDebtRate > minAssetRate)
                {
                    results.Add(new AnalysisResult(
                        Name,
                        "Value leak detected (Negative Arbitrage).",
                        $"You are earning {minAssetRate}% on certain assets while paying {maxDebtRate}% on debt. Moving funds from assets to pay debt would yield a guaranteed return.",
                        ImpactLevel.Warning
                    ));
                }
            }

            // 4. Debt Avalanche Guidance
            if (debts.Count >= 2)
            {
                var ordered = debts.OrderByDescending(d => d.InterestRate).ToList();
                var first = ordered[0].Label;
                var second = ordered[1].Label;
                results.Add(new AnalysisResult(
                    Name,
                    "Debt avalanche strategy recommended.",
                    $"To minimize total interest paid, concentrate extra payments on {first} first, then move to {second}.",
                    ImpactLevel.Info
                ));
            }

            return Task.FromResult<IEnumerable<AnalysisResult>>(results);
        }
    }
}
