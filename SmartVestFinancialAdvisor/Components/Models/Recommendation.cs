using SmartVestFinancialAdvisor.Components.Models;

namespace SmartVestFinancialAdvisor.Components.Models
{
    public sealed class Recommendation
    {
        public string Type { get; set; } = string.Empty;       // e.g., "Index Fund (S&P 500)"
        public string Risk { get; set; } = string.Empty;       // e.g., "Low", "Moderate", "High", "Very Low"
        public int Score10 { get; set; }                       // 0..10 recommendation strength
        public string? Info { get; set; }                      // description or helper text
        public string? Link { get; set; }                      // optional "learn more" URL

        // Convenience for the grid column
        public string RecDisplay => $"{Score10}/10";
    }
}