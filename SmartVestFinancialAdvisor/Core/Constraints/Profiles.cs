// File: Core/Constraints/Profiles.cs

using System;
using System.Collections.Generic;

namespace SmartVestFinancialAdvisor.Core.Constraints
{
    public static class Profiles
    {
        // Example predefined financial profiles for testing or defaults
        public static FinancialProfile GetSampleLowRiskProfile()
        {
            return new FinancialProfile
            {
                Age = 45,
                AnnualIncome = 50000,
                CurrentSavings = 20000,
                RiskTolerance = "Low",
                Notes = "Prefers conservative investments"
            };
        }

        public static FinancialProfile GetSampleMediumRiskProfile()
        {
            return new FinancialProfile
            {
                Age = 35,
                AnnualIncome = 90000,
                CurrentSavings = 15000,
                RiskTolerance = "Medium",
                Notes = "Balanced investor"
            };
        }

        public static FinancialProfile GetSampleHighRiskProfile()
        {
            return new FinancialProfile
            {
                Age = 28,
                AnnualIncome = 120000,
                CurrentSavings = 10000,
                RiskTolerance = "High",
                Notes = "Aggressive investor"
            };
        }

        // Helper method: Create a custom profile quickly
        public static FinancialProfile CreateCustomProfile(
            int age, decimal income, decimal savings, string riskTolerance, string notes = "")
        {
            return new FinancialProfile
            {
                Age = age,
                AnnualIncome = income,
                CurrentSavings = savings,
                RiskTolerance = riskTolerance,
                Notes = notes
            };
        }

        // Optional: Return a list of sample profiles
        public static List<FinancialProfile> GetSampleProfiles()
        {
            return new List<FinancialProfile>
            {
                GetSampleLowRiskProfile(),
                GetSampleMediumRiskProfile(),
                GetSampleHighRiskProfile()
            };
        }
    }
}