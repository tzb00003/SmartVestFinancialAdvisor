// File: Core/Constraints/Models.cs

using System;
using System.Collections.Generic;

namespace SmartVestFinancialAdvisor.Core.Constraints
{
    // Represents a user's financial profile
    public class FinancialProfile
    {
        // Age of the user
        public int Age { get; set; }

        // Annual income in dollars
        public decimal AnnualIncome { get; set; }

        // Current savings in dollars
        public decimal CurrentSavings { get; set; }

        // Risk tolerance: e.g., "Low", "Medium", "High"
        public string RiskTolerance { get; set; }

        // Optional: any additional notes
        public string Notes { get; set; }

        // Constructor (optional, can set default values)
        public FinancialProfile()
        {
            RiskTolerance = "Medium";
        }
    }

    // Represents the calculated financial score of a user
    public class FinancialScore
    {
        // Score value from 0-100
        public int ScoreValue { get; set; }

        // Risk-adjusted score (optional)
        public decimal RiskAdjustedScore { get; set; }

        // Date when the score was calculated
        public DateTime CalculatedAt { get; set; }

        // Constructor to initialize calculation time
        public FinancialScore()
        {
            CalculatedAt = DateTime.Now;
        }
    }

    // Represents portfolio constraints derived from user's profile
    public class PortfolioConstraints
    {
        // Maximum percentage allocation to stocks
        public decimal MaxStockAllocation { get; set; }

        // Maximum percentage allocation to bonds
        public decimal MaxBondAllocation { get; set; }

        // Maximum percentage allocation to cash
        public decimal MaxCashAllocation { get; set; }

        // Risk tolerance from profile (copied for reference)
        public string RiskTolerance { get; set; }

        // Constructor sets default allocations
        public PortfolioConstraints()
        {
            MaxStockAllocation = 0.6m;  // 60%
            MaxBondAllocation = 0.3m;   // 30%
            MaxCashAllocation = 0.1m;   // 10%
        }
    }
}