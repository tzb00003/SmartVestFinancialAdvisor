using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Constraints;
using SmartVestFinancialAdvisor.Core.Agents;
using SmartVestFinancialAdvisor.Core.Financial;
using SmartVestFinancialAdvisor.Infrastructure.Census;
using SmartVestFinancialAdvisor.Infrastructure.Benchmarks;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace SmartVestFinancialAdvisor
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== SmartVest Financial Advisor: System Integration Test ===\n");

            // 1. Setup Infrastructure
            string dbPath = Path.Combine(Environment.CurrentDirectory, "benchmarks.db");
            var benchmarkProvider = new SqliteBenchmarkProvider(dbPath);
            var aggregationService = new FinancialAggregationService();

            // 2. Initialize the Advisor Engine and Register Agents
            // In a production app, this would be handled by a DI container
            var advisorEngine = new AdvisorEngine();
            advisorEngine.RegisterAgent(new BenchmarkAgent());
            advisorEngine.RegisterAgent(new PortfolioAgent());
            advisorEngine.RegisterAgent(new SavingsAgent());

            // 3. Setup Logic Services
            var scoreCalculator = new ScoreCalculator(benchmarkProvider, aggregationService);
            var builder = new Builder(benchmarkProvider, scoreCalculator, advisorEngine);

            // 4. Create Sample Data (Example: High Earner with "Toxic" Debt)
            FinancialProfile financialProfile = new FinancialProfile
            {
                MonthlyIncome = 12000m, // High income ($144k/year)
                MonthlyExpense = 5000m,
                Savings = 15000m,       // Relatively low savings for this income
                Debt = 8000m,
                Age = 35,
                LocationState = "NY",
                RiskTolerance = 0.7m,
                Items = new List<FinancialItem>
                {
                    new FinancialItem { Label = "Checking", Amount = 15000m, IsDebt = false },
                    new FinancialItem { Label = "High-Interest Credit Card", Amount = 8000m, InterestRate = 22.5m, IsDebt = true },
                    new FinancialItem { Label = "Retirement Account", Amount = 45000m, IsRetirement = true, IsDebt = false }
                }
            };

            // 5. Ingest Census Data (Ensures the SQLite DB is populated for benchmarking)
            Console.WriteLine("--- Phase 1: Data Ingestion ---");
            var censusAgent = new CensusIngestionAgent(dbPath, Environment.CurrentDirectory);
            await censusAgent.RunIngestionAsync();
            Console.WriteLine("Census ingestion complete.\n");

            // 6. Execute the Builder (Orchestrates Scoring, Categorization, and Agent Analysis)
            Console.WriteLine("--- Phase 2: Processing Financial Strategy ---");
            BuildResult result = await builder.Build(financialProfile);

            // 7. Output Results
            Console.WriteLine($"\n[Calculation Results]");
            Console.WriteLine($"Total Financial Score: {result.Score.Total}/100");
            Console.WriteLine($"Assigned Category:    {result.Category}");

            Console.WriteLine("\n[Score Breakdown]");
            foreach (var subScore in result.Score.SubScores)
            {
                Console.WriteLine($" - {subScore.Name,-20}: {subScore.RawScore:F0}/100 (Weight: {subScore.Weight:P0})");
            }

            Console.WriteLine($"\n[Portfolio Constraints]");
            Console.WriteLine($"Stocks: {result.Constraints.MaxStockAllocation:P0} | Bonds: {result.Constraints.MaxBondAllocation:P0} | Cash: {result.Constraints.MaxCashAllocation:P0}");

            Console.WriteLine($"\n[Agent Insights & Recommendations]");
            if (!result.Insights.Any())
            {
                Console.WriteLine("No specific issues detected. Stay the course.");
            }
            else
            {
                foreach (var insight in result.Insights)
                {
                    // Color code output based on impact
                    var color = insight.Impact switch
                    {
                        ImpactLevel.Critical => ConsoleColor.Red,
                        ImpactLevel.Warning => ConsoleColor.Yellow,
                        _ => ConsoleColor.Cyan
                    };

                    Console.ForegroundColor = color;
                    Console.WriteLine($"[{insight.Impact.ToString().ToUpper()}] {insight.AgentName}");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"Insight: {insight.Insight}");
                    Console.WriteLine($"Advice:  {insight.Recommendation}\n");
                }
            }

            Console.WriteLine("=== Test Complete ===");
        }
    }
}