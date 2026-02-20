using System.Collections.Generic;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Benchmarks;

namespace SmartVestFinancialAdvisor.Core.Agents
{
    public class BenchmarkAgent : IAdvisorAgent
    {
        public string Name => "Demographic Benchmark Agent";

        public Task<IEnumerable<AnalysisResult>> AnalyzeAsync(ClientProfile profile, FinancialScore score, IncomeBenchmark? benchmark)
        {
            var results = new List<AnalysisResult>();
            if (benchmark == null) return Task.FromResult<IEnumerable<AnalysisResult>>(results);

            decimal annualIncome = profile.MonthlyIncome * 12;

            // P95 Tier - High Capacity
            if (annualIncome >= benchmark.P95)
            {
                results.Add(new AnalysisResult(
                    Name,
                    "Top-tier earnings detected (Top 5% of peers).",
                    "Your income power is exceptional for your demographic. Prioritize maxing out tax-advantaged accounts (401k/HSA) immediately.",
                    ImpactLevel.Info));
            }
            // Below P25 - Income Gap
            else if (annualIncome < benchmark.P25)
            {
                results.Add(new AnalysisResult(
                    Name,
                    "Income is below the 25th percentile for your region.",
                    "Your primary wealth hurdle is income volume. Focus on professional certification or side-income to increase investment capacity.",
                    ImpactLevel.Warning));
            }

            return Task.FromResult<IEnumerable<AnalysisResult>>(results);
        }
    }
}
