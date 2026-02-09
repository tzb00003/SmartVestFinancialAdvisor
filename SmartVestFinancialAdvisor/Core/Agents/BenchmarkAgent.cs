using System.Collections.Generic;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Benchmarks;

namespace SmartVestFinancialAdvisor.Core.Agents
{
    public class BenchmarkAgent : IAdvisorAgent
    {
        public string Name => "Benchmark Agent";

        public Task<IEnumerable<AnalysisResult>> AnalyzeAsync(ClientProfile profile, FinancialScore score, IncomeBenchmark? benchmark)
        {
            var results = new List<AnalysisResult>();

            if (benchmark != null)
            {
                decimal annualIncome = profile.MonthlyIncome * 12;

                if (annualIncome > benchmark.MedianIncome * 1.5m)
                {
                    results.Add(new AnalysisResult(
                        Name,
                        "Income significantly above peer median.",
                        "You may have capacity for more aggressive investments.",
                        ImpactLevel.Info
                    ));
                }
                else if (annualIncome < benchmark.MedianIncome * 0.8m)
                {
                    results.Add(new AnalysisResult(
                        Name,
                        "Income below peer median.",
                        "Focus on increasing income or reducing debt.",
                        ImpactLevel.Warning
                    ));
                }
            }

            return Task.FromResult<IEnumerable<AnalysisResult>>(results);
        }
    }
}
