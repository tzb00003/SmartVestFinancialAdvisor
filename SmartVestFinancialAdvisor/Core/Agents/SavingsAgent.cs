using System.Collections.Generic;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Benchmarks;

namespace SmartVestFinancialAdvisor.Core.Agents
{
    public class SavingsAgent : IAdvisorAgent
    {
        public string Name => "Savings Agent";

        public Task<IEnumerable<AnalysisResult>> AnalyzeAsync(ClientProfile profile, FinancialScore score, IncomeBenchmark? benchmark)
        {
            var results = new List<AnalysisResult>();

            // Rule 1: Emergency Fund Check
            decimal monthlyExpenses = profile.MonthlyIncome * 0.5m; // Approximation
            decimal monthsOfSavings = monthlyExpenses > 0 ? profile.Savings / monthlyExpenses : 0;

            if (monthsOfSavings < 3)
            {
                results.Add(new AnalysisResult(
                    Name,
                    $"Emergency fund is low ({monthsOfSavings:F1} months).",
                    "Aim for at least 3-6 months of expenses in savings.",
                    ImpactLevel.Critical
                ));
            }
            else if (monthsOfSavings >= 6)
            {
                results.Add(new AnalysisResult(
                    Name,
                    "Emergency fund is healthy.",
                    "Consider investing excess savings.",
                    ImpactLevel.Info
                ));
            }

            return Task.FromResult<IEnumerable<AnalysisResult>>(results);
        }
    }
}
