namespace SmartVestFinancialAdvisor.Core.Constraints
{
    /// <summary>
    /// Represents the target asset allocation constraints for a client's portfolio.
    /// These constraints are derived from the client's risk tolerance and financial score.
    /// </summary>
    public class PortfolioConstraints
    {
        /// <summary>The client's specific risk tolerance (0.0 to 1.0).</summary>
        public decimal RiskTolerance { get; set; }

        /// <summary>Maximum recommended allocation to stocks.</summary>
        public decimal MaxStockAllocation { get; set; }

        /// <summary>Maximum recommended allocation to bonds.</summary>
        public decimal MaxBondAllocation { get; set; }

        /// <summary>Maximum recommended allocation to cash/equivalents.</summary>
        public decimal MaxCashAllocation { get; set; }
    }
}