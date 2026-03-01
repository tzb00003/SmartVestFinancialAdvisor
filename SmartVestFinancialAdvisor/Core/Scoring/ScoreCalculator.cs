using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Benchmarks;
using SmartVestFinancialAdvisor.Core.Financial;

namespace SmartVestFinancialAdvisor.Core.Scoring
{
    public class ScoreCalculator
    {
        private readonly IBenchmarkProvider _benchmarkProvider;
        private readonly FinancialAggregationService _aggregationService;

        public ScoreCalculator(IBenchmarkProvider benchmarkProvider, FinancialAggregationService aggregationService)
        {
            _benchmarkProvider = benchmarkProvider;
            _aggregationService = aggregationService;
        }

        public async Task<ScoreResult> Calculate(ClientProfile client)
        {
            var subScores = await BuildSubScores(client);
            ValidateWeights(subScores);
            return new ScoreResult(subScores);
        }

        /// <summary>
        /// High-level wrapper that calculates the score and packages it for the UI/Categories.
        /// </summary>
        public async Task<FinancialScore> AggregateScore(ClientProfile client)
        {
            // 1. Run the actual calculation logic
            var result = await Calculate(client);

            // 2. Map it to the FinancialScore object
            return new FinancialScore
            {
                Total = result.TotalScore,
                SubScores = result.SubScores
            };
        }

        private async Task<List<SubScore>> BuildSubScores(ClientProfile client)
        {
            var aggregated = _aggregationService.Aggregate(client.Items ?? new List<FinancialItem>());

            return new List<SubScore>
            {
                // 40% - Earnings power (Now uses Percentile Ranking)
                new SubScore("Income Stability", await CalculateIncomeScore(client), 0.40m),
                
                // 20% - Solvency
                new SubScore("Savings Health", CalculateSavingsScore(client, aggregated), 0.20m),
                
                // 20% - Debt Load & Interest Toxicity
                new SubScore("Debt Load", CalculateDebtScore(client, aggregated), 0.20m),

                // 10% - Emergency Fund (Liquid Cash)
                new SubScore("Emergency Fund", CalculateEmergencyFundScore(client, aggregated), 0.10m),

                // 10% - Retirement Readiness (Fidelity Targets)
                new SubScore("Retirement Readiness", CalculateRetirementScore(client, aggregated), 0.10m)
            };
        }

        /// <summary>
        /// Evaluates income by ranking the user within their demographic percentile curve.
        /// </summary>
        private async Task<decimal> CalculateIncomeScore(ClientProfile client)
        {
            if (client.MonthlyIncome <= 0) return 0m;

            // 1. Fetch the benchmark row (B20018)
            var benchmark = await _benchmarkProvider.GetIncomeBenchmarkAsync(
                client.Age,
                client.LocationState ?? "NY",
                client.Gender
            );

            // Fallback: If no benchmark exists, use a national $100k median fallback
            if (benchmark == null)
            {
                decimal fallbackMonthlyMedian = 100000m / 12m;
                return Math.Min(100m, (client.MonthlyIncome / fallbackMonthlyMedian) * 50m);
            }

            decimal annualIncome = client.MonthlyIncome * 12m;

            // 2. Map user to the Percentile Curve
            // This creates a smoother 0-100 score based on where they sit
            if (annualIncome >= benchmark.P95) return 100m; // Top Tier
            if (annualIncome >= benchmark.P75) return 85m + ((annualIncome - benchmark.P75) / (benchmark.P95 - benchmark.P75) * 15m);
            if (annualIncome >= benchmark.MedianIncome) return 60m + ((annualIncome - benchmark.MedianIncome) / (benchmark.P75 - benchmark.MedianIncome) * 25m);
            if (annualIncome >= benchmark.P25) return 30m + ((annualIncome - benchmark.P25) / (benchmark.MedianIncome - benchmark.P25) * 30m);

            // Below P25
            return (annualIncome / benchmark.P25) * 30m;
        }

        // ... [Savings, Debt, Emergency Fund methods remain largely the same] ...

        private decimal CalculateSavingsScore(ClientProfile client, AggregatedFinancials aggregated)
        {
            var savings = aggregated.TotalAssets;
            var debt = aggregated.TotalDebt;
            if (debt <= 0) return 100m;
            if (savings <= 0) return 0m;
            return Math.Min(100m, (savings / debt) * 100m);
        }

        private decimal CalculateDebtScore(ClientProfile client, AggregatedFinancials aggregated)
        {
            decimal totalOutflow = client.MonthlyExpense + aggregated.TotalMonthlyDebtPayments;
            decimal debtRatio = totalOutflow / Math.Max(client.MonthlyIncome, 1);
            decimal cashFlowScore = Math.Max(0m, (1 - (debtRatio / 0.6m)) * 100m);

            decimal interestPenalty = 1.0m;
            if (aggregated.WeightedDebtRate > 7.0m)
            {
                decimal penalty = aggregated.WeightedDebtRate - 7.0m;
                interestPenalty = Math.Max(0.2m, 1.0m - (penalty * 0.05m));
            }
            return cashFlowScore * interestPenalty;
        }

        private decimal CalculateEmergencyFundScore(ClientProfile client, AggregatedFinancials aggregated)
        {
            decimal target = client.MonthlyExpense * 6m;
            if (target <= 0) return 100m;
            return Math.Min(100m, (aggregated.TotalLiquidAssets / target) * 100m);
        }

        private decimal CalculateRetirementScore(ClientProfile client, AggregatedFinancials aggregated)
        {
            decimal ageMultiplier = client.Age < 30 ? 1m : (client.Age - 20) / 5m;
            decimal target = client.MonthlyIncome * 12m * ageMultiplier;
            return Math.Min(100m, (aggregated.TotalRetirementSavings / Math.Max(target, 1)) * 100m);
        }

        private void ValidateWeights(IEnumerable<SubScore> subScores)
        {
            if (subScores.Sum(s => s.Weight) != 1.0m)
                throw new InvalidOperationException("Score weights must total 1.0.");
        }

        /// <summary>
        /// Generates a "Peer Avatar" using our updated 4-percentile model.
        /// </summary>
        public ClientProfile CreatePeerAvatar(IncomeBenchmark benchmark, string tier)
        {
            decimal annualIncome = tier switch
            {
                "P25" => benchmark.P25,
                "P50" => benchmark.MedianIncome,
                "P75" => benchmark.P75,
                "P95" => benchmark.P95,
                _ => benchmark.MedianIncome
            };

            decimal monthly = annualIncome / 12m;
            return new ClientProfile
            {
                MonthlyIncome = monthly,
                Age = (benchmark.AgeRangeMin + benchmark.AgeRangeMax) / 2,
                LocationState = benchmark.State,
                Gender = benchmark.Gender,
                MonthlyExpense = monthly * 0.45m,
                Items = new List<FinancialItem>
                {
                    new FinancialItem { Label = "Checking", Amount = monthly * 2, IsDebt = false },
                    new FinancialItem { Label = "Debt", Amount = monthly * 1, MonthlyPayment = monthly * 0.1m, InterestRate = 0.07m, IsDebt = true }
                }
            };
        }
    }
}
