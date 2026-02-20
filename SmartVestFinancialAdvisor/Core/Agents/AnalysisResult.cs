namespace SmartVestFinancialAdvisor.Core.Agents
{
    public enum ImpactLevel { Info, Warning, Critical }

    public class AnalysisResult
    {
        public string AgentName { get; init; }
        public string Insight { get; init; }
        public string Recommendation { get; init; }
        public ImpactLevel Impact { get; init; }

        public AnalysisResult(string agentName, string insight, string recommendation, ImpactLevel impact)
        {
            AgentName = agentName;
            Insight = insight;
            Recommendation = recommendation;
            Impact = impact;
        }
    }
}
