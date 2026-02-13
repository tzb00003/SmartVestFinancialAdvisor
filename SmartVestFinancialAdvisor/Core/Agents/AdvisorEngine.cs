using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Benchmarks;

namespace SmartVestFinancialAdvisor.Core.Agents
{
    /// <summary>
    /// The central orchestrator that manages multiple analysis agents and consolidates their findings.
    /// </summary>
    public class AdvisorEngine
    {
        private readonly List<IAdvisorAgent> _agents = new();

        /// <summary>
        /// Registers a new specialized analysis agent into the engine.
        /// </summary>
        /// <param name="agent">The agent to register.</param>
        public void RegisterAgent(IAdvisorAgent agent)
        {
            _agents.Add(agent);
        }

        /// <summary>
        /// Executes all registered agents against a client profile and sorts the aggregate results by impact.
        /// </summary>
        /// <param name="profile">The client's financial and demographic data.</param>
        /// <param name="score">The client's calculated aggregate score.</param>
        /// <param name="benchmark">Optional regional benchmark for context.</param>
        /// <returns>A collection of <see cref="AnalysisResult"/>s ordered by critical impact.</returns>
        public async Task<IEnumerable<AnalysisResult>> RunAnalysisAsync(ClientProfile profile, FinancialScore score, IncomeBenchmark? benchmark)
        {
            var results = new List<AnalysisResult>();

            // Execute all agents in parallel
            var tasks = _agents.Select(async agent =>
            {
                var agentResults = await agent.AnalyzeAsync(profile, score, benchmark);
                foreach (var result in agentResults)
                {
                    lock (results) // Consolidate results in a thread-safe manner
                    {
                        results.Add(result);
                    }
                }
            });

            await Task.WhenAll(tasks);

            // Order by impact level (Critical first, then Warning, then Info)
            return results.OrderByDescending(r => r.Impact).ToList();
        }
    }
}
