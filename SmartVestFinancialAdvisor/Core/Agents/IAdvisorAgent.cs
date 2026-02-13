using System.Collections.Generic;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Benchmarks;

namespace SmartVestFinancialAdvisor.Core.Agents
{
    /// <summary>
    /// Defines the contract for specialized intelligence agents that analyze client data.
    /// </summary>
    public interface IAdvisorAgent
    {
        /// <summary>The display name of the advisor agent.</summary>
        string Name { get; }

        /// <summary>
        /// Performs specialized analysis on the client profile and state.
        /// </summary>
        /// <param name="profile">The client's raw financial data.</param>
        /// <param name="score">The calculated aggregate score for context.</param>
        /// <param name="benchmark">Optional regional benchmark data.</param>
        /// <returns>A collection of findings and recommendations.</returns>
        Task<IEnumerable<AnalysisResult>> AnalyzeAsync(ClientProfile profile, FinancialScore score, IncomeBenchmark? benchmark);
    }
}
