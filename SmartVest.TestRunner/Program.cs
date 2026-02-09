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

            // 1. Create a sample client profile
            ClientProfile clientProfile = new ClientProfile
            {
                MonthlyIncome = 6000m,
                Savings = 20000m,
                MonthlyDebt = 1500m,
                Age = 30,
                LocationState = "NY",
                Gender = null
            };

            // 2. Create a FinancialProfile for constraints builder
            FinancialProfile financialProfile = new FinancialProfile
            {
                MonthlyIncome = clientProfile.MonthlyIncome,
                Savings = clientProfile.Savings,
                MonthlyDebt = clientProfile.MonthlyDebt,
                RiskTolerance = 0.5m // example risk tolerance
            };

            // 3. Calculate scores
            ScoreCalculator scoreCalculator = new ScoreCalculator();
            FinancialScore financialScore = scoreCalculator.AggregateScore(clientProfile);

            Console.WriteLine("--- SubScores ---");
            foreach (var sub in financialScore.SubScores)
            {
                Console.WriteLine($"{sub.Name}: Raw={sub.RawScore}, Weight={sub.Weight}, Weighted={sub.WeightedScore}");
            }

            Console.WriteLine($"\nTotal Score: {financialScore.Total}");

            // 4. Determine client category
            ClientCategory category = Categories.DetermineCategory(financialScore, clientProfile);
            var categoryDefinition = Categories.GetCategoryDefinition(category);

            Console.WriteLine($"\nClient Category: {category}");
            Console.WriteLine($"Category Rules: Stock={categoryDefinition.DefaultStockAllocation}, Bond={categoryDefinition.DefaultBondAllocation}, Cash={categoryDefinition.DefaultCashAllocation}");

            // 5. Build portfolio constraints
            Builder builder = new Builder();
            BuildResult buildResult = builder.Build(financialProfile);

            Console.WriteLine("\n--- Portfolio Constraints ---");
            Console.WriteLine($"Max Stock Allocation: {buildResult.Constraints.MaxStockAllocation}");
            Console.WriteLine($"Max Bond Allocation: {buildResult.Constraints.MaxBondAllocation}");
            Console.WriteLine($"Max Cash Allocation: {buildResult.Constraints.MaxCashAllocation}");

            // 6. Census Ingestion (Trigger Agent first)
            Console.WriteLine("\n--- Census Data Agent ---");
            Console.WriteLine("Running ingestion (this calls the live Census API)...");
            string dbPath = Path.Combine(Environment.CurrentDirectory, "benchmarks.db");
            Console.WriteLine($"DB path: {dbPath}");
            var censusAgent = new CensusIngestionAgent(dbPath, Environment.CurrentDirectory);
            await censusAgent.RunIngestionAsync();

            // 7. Test Benchmarks (after ingestion)
            Console.WriteLine("\n--- Benchmarks ---");
            var benchmarkProvider = new SqliteBenchmarkProvider(dbPath);
            var benchmark = await benchmarkProvider.GetIncomeBenchmarkAsync(clientProfile.Age, clientProfile.LocationState ?? "NY");

            if (benchmark != null)
            {
                Console.WriteLine($"Found Benchmark for {clientProfile.LocationState}, Age {clientProfile.Age}: Median=${benchmark.MedianIncome}, Avg=${benchmark.AverageIncome}");
                if (clientProfile.MonthlyIncome * 12 < benchmark.MedianIncome)
                {
                    Console.WriteLine("Client is below median income for their demographic.");
                }
                else
                {
                    Console.WriteLine("Client is above median income for their demographic.");
                }
            }
            else
            {
                Console.WriteLine("No benchmark found.");
            }

            // 8. Re-evaluate Category with Benchmark
            Console.WriteLine("\n--- Benchmark-Adjusted Categorization ---");
            // Original category was calculated without benchmark in step 4 (which we didn't update yet, let's update it here)
            ClientCategory adjustedCategory = Categories.DetermineCategory(financialScore, clientProfile, benchmark);
            var adjustedDefinition = Categories.GetCategoryDefinition(adjustedCategory);

            Console.WriteLine($"Original Category: {category} (Rules: Stock={categoryDefinition.DefaultStockAllocation})");
            Console.WriteLine($"Adjusted Category: {adjustedCategory} (Rules: Stock={adjustedDefinition.DefaultStockAllocation})");

            if (category != adjustedCategory)
            {
                Console.WriteLine(">> Category matched due to benchmark comparison!");
            }
            else
            {
                Console.WriteLine(">> Category remained the same.");
            }

            Console.WriteLine("\n=== Test Complete ===");
        }
    }
}
