using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Benchmarks;

namespace SmartVestFinancialAdvisor.Core.Agents
{
    public class AdvisorEngine
    {
        private readonly List<IAdvisorAgent> _agents = new();

        public void RegisterAgent(IAdvisorAgent agent) => _agents.Add(agent);

        public async Task<IEnumerable<AnalysisResult>> RunAnalysisAsync(
            ClientProfile profile,
            FinancialScore score,
            IncomeBenchmark? benchmark)
        {
            var results = new List<AnalysisResult>();

            // Execute all agents in parallel for maximum performance
            var tasks = _agents.Select(async agent =>
            {
                var agentResults = await agent.AnalyzeAsync(profile, score, benchmark);
                if (agentResults != null)
                {
                    lock (results)
                    {
                        results.AddRange(agentResults);
                    }
                }
            });

            await Task.WhenAll(tasks);

            // Prioritize Critical issues first, then Warnings, then Info
            return results.OrderByDescending(r => r.Impact).ToList();
        }
    }
}
