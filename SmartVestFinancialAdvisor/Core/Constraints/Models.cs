namespace SmartVestFinancialAdvisor.Core.Constraints
{
    public class PortfolioConstraints
    {
        public decimal RiskTolerance { get; set; }
        public decimal MaxStockAllocation { get; set; }
        public decimal MaxBondAllocation { get; set; }
        public decimal MaxCashAllocation { get; set; }
    }
}