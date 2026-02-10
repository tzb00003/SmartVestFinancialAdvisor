using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Constraints;
using SmartVestFinancialAdvisor.Core.Agents;
using SmartVestFinancialAdvisor.Infrastructure.Census;
using SmartVestFinancialAdvisor.Infrastructure.Benchmarks;
using System.Threading.Tasks;
using System;
using System.IO;

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

            // 2. Create a sample client profile
            ClientProfile clientProfile = new ClientProfile
            {
                MonthlyIncome = 6000m,
                Savings = 20000m,
                MonthlyDebt = 1500m,
                Age = 30,
                LocationState = "NY",
                Gender = null
            };

            // 3. Create a FinancialProfile for constraints builder
            FinancialProfile financialProfile = new FinancialProfile
            {
                MonthlyIncome = clientProfile.MonthlyIncome,
                Savings = clientProfile.Savings,
                MonthlyDebt = clientProfile.MonthlyDebt,
                RiskTolerance = 0.5m // example risk tolerance
            };

            // 4. Ingest Census Data first (so scoring can use it)
            Console.WriteLine("\n--- Census Data Agent ---");
            Console.WriteLine("Running ingestion (this calls the live Census API)...");
            var censusAgent = new CensusIngestionAgent(dbPath, Environment.CurrentDirectory);
            await censusAgent.RunIngestionAsync();

            // 5. Calculate scores (Async now)
            ScoreCalculator scoreCalculator = new ScoreCalculator(benchmarkProvider);
            FinancialScore financialScore = await scoreCalculator.AggregateScore(clientProfile);

            Console.WriteLine("--- SubScores ---");
            foreach (var sub in financialScore.SubScores)
            {
                Console.WriteLine($"{sub.Name}: Raw={sub.RawScore}, Weight={sub.Weight}, Weighted={sub.WeightedScore}");
            }

            Console.WriteLine($"\nTotal Score: {financialScore.Total}");

            // 6. Determine client category
            ClientCategory category = Categories.DetermineCategory(financialScore, clientProfile);
            var categoryDefinition = Categories.GetCategoryDefinition(category);

            Console.WriteLine($"\nClient Category: {category}");
            Console.WriteLine($"Category Rules: Stock={categoryDefinition.DefaultStockAllocation}, Bond={categoryDefinition.DefaultBondAllocation}, Cash={categoryDefinition.DefaultCashAllocation}");

            // 7. Build portfolio constraints (Async now)
            Builder builder = new Builder(benchmarkProvider);
            BuildResult buildResult = await builder.Build(financialProfile);

            Console.WriteLine("\n--- Portfolio Constraints ---");
            Console.WriteLine($"Max Stock Allocation: {buildResult.Constraints.MaxStockAllocation}");
            Console.WriteLine($"Max Bond Allocation: {buildResult.Constraints.MaxBondAllocation}");
            Console.WriteLine($"Max Cash Allocation: {buildResult.Constraints.MaxCashAllocation}");

            // 8. Test Benchmarks (Manual check of what we just used)
            Console.WriteLine("\n--- Benchmark Check ---");
            var benchmark = await benchmarkProvider.GetIncomeBenchmarkAsync(clientProfile.Age, clientProfile.LocationState ?? "NY");

            if (benchmark != null)
            {
                Console.WriteLine($"Found Benchmark for {clientProfile.LocationState}, Age {clientProfile.Age}: Median=${benchmark.MedianIncome}, Avg=${benchmark.AverageIncome}");
            }

            // 9. Re-evaluate Category with Benchmark (Categories.DetermineCategory already does this if benchmark is passed)
            ClientCategory adjustedCategory = Categories.DetermineCategory(financialScore, clientProfile, benchmark);
            var adjustedDefinition = Categories.GetCategoryDefinition(adjustedCategory);

            Console.WriteLine($"\nAdjusted Category Check: {adjustedCategory}");


            Console.WriteLine("\n=== Test Complete ===");
        }
    }
}
