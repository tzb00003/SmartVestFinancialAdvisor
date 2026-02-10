namespace SmartVestFinancialAdvisor.Core.Constraints
{
    /// <summary>
    /// Represents the output of the Builder.
    /// </summary>
    public class PortfolioConstraints
    {
        public decimal RiskTolerance { get; set; }
        public decimal MaxStockAllocation { get; set; }
        public decimal MaxBondAllocation { get; set; }
        public decimal MaxCashAllocation { get; set; }
    }
}