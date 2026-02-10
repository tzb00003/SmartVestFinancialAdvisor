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
        public string Name => "Portfolio Agent";

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

            var toxicDebt = debts
                .Where(d => d.InterestRate > 7m)
                .OrderByDescending(d => d.InterestRate)
                .ToList();

            foreach (var debt in toxicDebt)
            {
                results.Add(new AnalysisResult(
                    Name,
                    $"Toxic debt detected: {debt.Label} at {debt.InterestRate}%.",
                    $"URGENT: Pay off {debt.Label}. Its {debt.InterestRate}% rate is destroying your wealth.",
                    ImpactLevel.Critical
                ));
            }

            if (!assets.Any(a => a.Label.Trim().Equals("Emergency Fund", System.StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(new AnalysisResult(
                    Name,
                    "No designated emergency fund found.",
                    "ADVICE: You lack a designated Emergency Fund. Aim for 3 months of expenses in Cash.",
                    ImpactLevel.Warning
                ));
            }

            if (assets.Count > 0 && debts.Count > 0)
            {
                var maxDebtRate = debts.Max(d => d.InterestRate);
                var minAssetRate = assets.Min(a => a.InterestRate);

                if (maxDebtRate > minAssetRate)
                {
                    results.Add(new AnalysisResult(
                        Name,
                        "Value leak detected between assets and debts.",
                        $"You are earning {minAssetRate}% on assets while paying {maxDebtRate}% on debt. Prioritize debt payoff.",
                        ImpactLevel.Warning
                    ));
                }
            }

            if (debts.Count >= 2)
            {
                var ordered = debts.OrderByDescending(d => d.InterestRate).ToList();
                var first = ordered[0].Label;
                var second = ordered[1].Label;
                results.Add(new AnalysisResult(
                    Name,
                    "Debt avalanche order identified.",
                    $"Prioritize paying off {first} first, then {second}.",
                    ImpactLevel.Info
                ));
            }

            return Task.FromResult<IEnumerable<AnalysisResult>>(results);
        }
    }
}
