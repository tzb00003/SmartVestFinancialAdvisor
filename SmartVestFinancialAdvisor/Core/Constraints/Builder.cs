// File: Core/Constraints/Builder.cs

using System;

namespace SmartVestFinancialAdvisor.Core.Constraints
{
    public class Builder
    {
        // Method to calculate financial score from a profile
        public FinancialScore CalculateScore(FinancialProfile profile)
        {
            // Create a new FinancialScore object
            var score = new FinancialScore();

            // Example logic: score out of 100 based on age and income
            // 50% weight for income, 50% weight for savings
            decimal incomeScore = Math.Min(profile.AnnualIncome / 1000, 50);  // cap at 50
            decimal savingsScore = Math.Min(profile.CurrentSavings / 1000, 50); // cap at 50

            score.ScoreValue = (int)(incomeScore + savingsScore);

            // Adjust score based on risk tolerance
            switch (profile.RiskTolerance)
            {
                case "High":
                    score.RiskAdjustedScore = score.ScoreValue * 1.1m;  // +10%
                    break;
                case "Low":
                    score.RiskAdjustedScore = score.ScoreValue * 0.9m;  // -10%
                    break;
                default:
                    score.RiskAdjustedScore = score.ScoreValue;         // Medium: no change
                    break;
            }

            return score;
        }

        // Method to generate portfolio constraints from profile
        public PortfolioConstraints GenerateConstraints(FinancialProfile profile)
        {
            var constraints = new PortfolioConstraints
            {
                RiskTolerance = profile.RiskTolerance
            };

            // Example logic: adjust allocations based on risk
            switch (profile.RiskTolerance)
            {
                case "High":
                    constraints.MaxStockAllocation = 0.8m;
                    constraints.MaxBondAllocation = 0.15m;
                    constraints.MaxCashAllocation = 0.05m;
                    break;
                case "Low":
                    constraints.MaxStockAllocation = 0.3m;
                    constraints.MaxBondAllocation = 0.5m;
                    constraints.MaxCashAllocation = 0.2m;
                    break;
                default: // Medium
                    constraints.MaxStockAllocation = 0.6m;
                    constraints.MaxBondAllocation = 0.3m;
                    constraints.MaxCashAllocation = 0.1m;
                    break;
            }

            return constraints;
        }

        // Optional: method to run both steps together
        public (FinancialScore score, PortfolioConstraints constraints) Build(FinancialProfile profile)
        {
            var score = CalculateScore(profile);
            var constraints = GenerateConstraints(profile);
            return (score, constraints);
        }
    }
}