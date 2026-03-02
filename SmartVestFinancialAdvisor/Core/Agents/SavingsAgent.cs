using System.Collections.Generic;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Benchmarks;

namespace SmartVestFinancialAdvisor.Core.Agents
{
    public class SavingsAgent : IAdvisorAgent
    {
        public string Name => "Liquidity Agent";

        public Task<IEnumerable<AnalysisResult>> AnalyzeAsync(ClientProfile profile, FinancialScore score, IncomeBenchmark? benchmark)
        {
            var results = new List<AnalysisResult>();

            decimal monthlyExpenses = profile.MonthlyExpense > 0 ? profile.MonthlyExpense : profile.MonthlyIncome * 0.7m;
            decimal safetyNetMonths = profile.Savings / Math.Max(monthlyExpenses, 1);

            // Rule: Low coverage is always critical
            if (safetyNetMonths < 3)
            {
                results.Add(new AnalysisResult(
                    Name,
                    $"Fragile Liquidity ({safetyNetMonths:F1} months).",
                    "Your cash reserves are dangerously low. Pause all non-essential spending until you have a 3-month cushion.",
                    ImpactLevel.Critical));
            }
            // Rule: If score is high (disciplined) and savings are very high, avoid "Cash Drag"
            else if (safetyNetMonths > 12 && score.Total > 80)
            {
                results.Add(new AnalysisResult(
                    Name,
                    "Excessive Cash Reserves.",
                    "Your financial score is strong and you have over a year of cash. Consider moving the excess into a brokerage account to avoid inflation drag.",
                    ImpactLevel.Info));
            }

            return Task.FromResult<IEnumerable<AnalysisResult>>(results);
        }
    }
}
