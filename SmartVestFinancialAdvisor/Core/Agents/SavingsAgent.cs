using System.Collections.Generic;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Benchmarks;

namespace SmartVestFinancialAdvisor.Core.Agents
{
    /// <summary>
    /// An advisor agent that evaluates the client's liquidity and short-term financial safety net.
    /// </summary>
    public class SavingsAgent : IAdvisorAgent
    {
        /// <inheritdoc/>
        public string Name => "Savings Agent";

        /// <summary>
        /// Analyzes the client's savings versus their expenses to determine emergency fund adequacy.
        /// </summary>
        public Task<IEnumerable<AnalysisResult>> AnalyzeAsync(ClientProfile profile, FinancialScore score, IncomeBenchmark? benchmark)
        {
            var results = new List<AnalysisResult>();

            // Rule 1: Emergency Fund Coverage Check
            // Fallback to 50% of income if expense data is missing
            decimal monthlyExpenses = profile.MonthlyExpense > 0 ? profile.MonthlyExpense : profile.MonthlyIncome * 0.5m;
            decimal monthsOfSavings = monthlyExpenses > 0 ? profile.Savings / monthlyExpenses : 0;

            if (monthsOfSavings < 3)
            {
                results.Add(new AnalysisResult(
                    Name,
                    $"Emergency fund is critically low ({monthsOfSavings:F1} months).",
                    "Safety net is insufficient. Aim to save at least 3-6 months of expenses before aggressive investing.",
                    ImpactLevel.Critical
                ));
            }
            else if (monthsOfSavings >= 6)
            {
                results.Add(new AnalysisResult(
                    Name,
                    "Emergency fund is healthy.",
                    "You have a robust cash cushion (6+ months). Any additional savings could be deployed into higher-growth investments.",
                    ImpactLevel.Info
                ));
            }

            return Task.FromResult<IEnumerable<AnalysisResult>>(results);
        }
    }
}
