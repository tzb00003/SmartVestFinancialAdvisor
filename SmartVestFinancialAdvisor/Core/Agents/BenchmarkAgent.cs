using System.Collections.Generic;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Benchmarks;

namespace SmartVestFinancialAdvisor.Core.Agents
{
    /// <summary>
    /// An advisor agent that identifies insights by comparing client income against regional benchmarks.
    /// </summary>
    public class BenchmarkAgent : IAdvisorAgent
    {
        /// <inheritdoc/>
        public string Name => "Benchmark Agent";

        /// <summary>
        /// Analyzes client income relative to peer data to identify wealth gaps or excess capacity.
        /// </summary>
        public Task<IEnumerable<AnalysisResult>> AnalyzeAsync(ClientProfile profile, FinancialScore score, IncomeBenchmark? benchmark)
        {
            var results = new List<AnalysisResult>();

            if (benchmark != null)
            {
                decimal annualIncome = profile.MonthlyIncome * 12;

                // High earner detection (>50% above median)
                if (annualIncome > benchmark.MedianIncome * 1.5m)
                {
                    results.Add(new AnalysisResult(
                        Name,
                        "Income significantly above peer median.",
                        "You may have capacity for more aggressive investments based on your income power.",
                        ImpactLevel.Info
                    ));
                }
                // Under-performer detection (20% below median)
                else if (annualIncome < benchmark.MedianIncome * 0.8m)
                {
                    results.Add(new AnalysisResult(
                        Name,
                        "Income below peer median.",
                        "Relative to your peers in this region/age group, your income is lower. Focus on professional development or debt reduction.",
                        ImpactLevel.Warning
                    ));
                }
            }

            return Task.FromResult<IEnumerable<AnalysisResult>>(results);
        }
    }
}
