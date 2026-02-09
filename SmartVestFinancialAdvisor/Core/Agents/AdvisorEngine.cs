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

        public void RegisterAgent(IAdvisorAgent agent)
        {
            _agents.Add(agent);
        }

        public async Task<IEnumerable<AnalysisResult>> RunAnalysisAsync(ClientProfile profile, FinancialScore score, IncomeBenchmark? benchmark)
        {
            var results = new List<AnalysisResult>();

            var tasks = _agents.Select(async agent =>
            {
                var agentResults = await agent.AnalyzeAsync(profile, score, benchmark);
                foreach (var result in agentResults)
                {
                    lock (results) // Simple thread safety for list add
                    {
                        results.Add(result);
                    }
                }
            });

            await Task.WhenAll(tasks);

            // Order by impact (Critical first)
            return results.OrderByDescending(r => r.Impact).ToList();
        }
    }
}
