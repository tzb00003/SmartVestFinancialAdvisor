namespace SmartVestFinancialAdvisor.Core.Financial
{
    /// <summary>
    /// Represents a single financial item (asset or debt).
    /// </summary>
    public class FinancialItem
    {
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal InterestRate { get; set; }
        public bool IsDebt { get; set; }
    }
}
