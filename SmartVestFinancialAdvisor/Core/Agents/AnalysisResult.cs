namespace SmartVestFinancialAdvisor.Core.Agents
{
    public class AnalysisResult
    {
        public string AgentName { get; set; }
        public string Insight { get; set; }
        public string Recommendation { get; set; }
        public ImpactLevel Impact { get; set; }

        public AnalysisResult(string agentName, string insight, string recommendation, ImpactLevel impact)
        {
            AgentName = agentName;
            Insight = insight;
            Recommendation = recommendation;
            Impact = impact;
        }
    }

    public enum ImpactLevel
    {
        Info,
        Warning,
        Critical
    }
}
