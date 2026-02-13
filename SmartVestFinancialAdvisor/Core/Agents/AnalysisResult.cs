namespace SmartVestFinancialAdvisor.Core.Agents
{
    /// <summary>
    /// Represents a specific insight or recommendation produced by an analysis agent.
    /// </summary>
    public class AnalysisResult
    {
        /// <summary>The name of the agent that generated this finding.</summary>
        public string AgentName { get; set; }

        /// <summary>The core observation or insight derived from the data.</summary>
        public string Insight { get; set; }

        /// <summary>The specific action or advice recommended to the client.</summary>
        public string Recommendation { get; set; }

        /// <summary>The severity or priority of this finding.</summary>
        public ImpactLevel Impact { get; set; }

        /// <summary>
        /// Initializes a new instance of an AnalysisResult.
        /// </summary>
        public AnalysisResult(string agentName, string insight, string recommendation, ImpactLevel impact)
        {
            AgentName = agentName;
            Insight = insight;
            Recommendation = recommendation;
            Impact = impact;
        }
    }

    /// <summary>
    /// Categorizes the urgency and priority of an analysis finding.
    /// </summary>
    public enum ImpactLevel
    {
        /// <summary>Informational insight with no immediate action required.</summary>
        Info,
        /// <summary>A potential issue that should be monitored or addressed eventually.</summary>
        Warning,
        /// <summary>A high-priority issue requiring immediate attention.</summary>
        Critical
    }
}
