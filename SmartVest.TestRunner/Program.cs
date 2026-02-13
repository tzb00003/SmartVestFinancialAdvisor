using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Constraints;
using SmartVestFinancialAdvisor.Core.Agents;
using SmartVestFinancialAdvisor.Core.Financial;
using SmartVestFinancialAdvisor.Infrastructure.Census;
using SmartVestFinancialAdvisor.Infrastructure.Benchmarks;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Collections.Generic; // Added for List<>

namespace SmartVestFinancialAdvisor
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== SmartVestFinancialAdvisor Test ===\n");

            // 1. Setup Benchmark Provider and database
            string dbPath = Path.Combine(Environment.CurrentDirectory, "benchmarks.db");
            var benchmarkProvider = new SqliteBenchmarkProvider(dbPath);
            var aggregationService = new FinancialAggregationService();

            // 2. Create a sample client profile (Updated with Items list for scoring)
            ClientProfile clientProfile = new ClientProfile
            {
                MonthlyIncome = 6000m,
                Savings = 20000m,
                Debt = 1500m,
                Age = 30,
                LocationState = "OH",
                Gender = null,
                Items = new List<FinancialItem>
                {
                    new FinancialItem { Label = "Checking", Amount = 5000m, IsDebt = false, IsRetirement = false },
                    new FinancialItem { Label = "401k", Amount = 30000m, IsDebt = false, IsRetirement = true },
                    new FinancialItem { Label = "Car Loan", Amount = 1500m, MonthlyPayment = 300m, InterestRate = 0.05m, IsDebt = true }
                }
            };

            // 3. Create a FinancialProfile for constraints builder
            FinancialProfile financialProfile = new FinancialProfile
            {
                MonthlyIncome = clientProfile.MonthlyIncome,
                Savings = clientProfile.Savings,
                Debt = clientProfile.Debt,
                Age = clientProfile.Age,
                LocationState = clientProfile.LocationState,
                Items = clientProfile.Items,
                RiskTolerance = 0.5m
            };

            // 4. Ingest Census Data first
            Console.WriteLine("\n--- Census Data Agent ---");
            var censusAgent = new CensusIngestionAgent(dbPath, Environment.CurrentDirectory);
            await censusAgent.RunIngestionAsync();

            // 5. Calculate scores
            ScoreCalculator scoreCalculator = new ScoreCalculator(benchmarkProvider, aggregationService);
            FinancialScore financialScore = await scoreCalculator.AggregateScore(clientProfile);

            Console.WriteLine("--- SubScores ---");
            foreach (var sub in financialScore.SubScores)
            {
                Console.WriteLine($"{sub.Name}: Raw={sub.RawScore}, Weighted={sub.WeightedScore}");
            }
            Console.WriteLine($"\nTotal Score: {financialScore.Total}");

            // 6. Determine client category (FIXED: Now uses the Peer-Comparison Async method)
            Console.WriteLine("\n--- Categorization ---");
            ClientCategory category = await Categories.DetermineCategoryAsync(
                clientProfile,
                scoreCalculator,
                benchmarkProvider
            );

            var categoryDefinition = Categories.GetCategoryDefinition(category);

            Console.WriteLine($"Client Category (Peer-Based): {category}");
            Console.WriteLine($"Rules: Stock={categoryDefinition.DefaultStockAllocation}, Bond={categoryDefinition.DefaultBondAllocation}");

            // 7. Build portfolio constraints (Using the Builder class)
            Builder builder = new Builder(benchmarkProvider);
            BuildResult buildResult = await builder.Build(financialProfile);

            Console.WriteLine("\n--- Portfolio Constraints ---");
            Console.WriteLine($"Final Category: {buildResult.Category}");
            Console.WriteLine($"Max Stock Allocation: {buildResult.Constraints.MaxStockAllocation}");

            Console.WriteLine("\n=== Test Complete ===");
        }
    }
}