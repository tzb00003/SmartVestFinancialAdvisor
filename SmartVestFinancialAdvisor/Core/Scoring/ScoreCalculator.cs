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
            var result = await Calculate(client);

            return new FinancialScore
            {
                Total = result.TotalScore,
                SubScores = result.SubScores
            };
        }

        private async Task<List<SubScore>> BuildSubScores(ClientProfile client)
        {
            var items = client.Items ?? new List<FinancialItem>();
            var aggregated = _aggregationService.Aggregate(items);

            // ---- Fallbacks when Items are missing or incomplete ----
            // Many users provide totals (Savings/Debt) without item breakdown.
            // These fallbacks prevent Emergency/Savings/Retirement scores from incorrectly being 0.
            var effectiveLiquidAssets =
                aggregated.TotalLiquidAssets > 0m ? aggregated.TotalLiquidAssets :
                (client.Savings > 0m ? client.Savings : 0m);

            var effectiveTotalAssets =
                aggregated.TotalAssets > 0m ? aggregated.TotalAssets :
                (client.Savings > 0m ? client.Savings : 0m);

            var effectiveMonthlyDebtPayments =
                aggregated.TotalMonthlyDebtPayments > 0m ? aggregated.TotalMonthlyDebtPayments :
                (client.Debt > 0m ? client.Debt : 0m);

            var effectiveTotalDebtBalance =
                aggregated.TotalDebt > 0m ? aggregated.TotalDebt : 0m;

            var effectiveWeightedDebtRate =
                effectiveTotalDebtBalance > 0m ? aggregated.WeightedDebtRate : 0m;

            // Carry a derived "effective aggregated" set for downstream calcs (without changing the record type)
            var eff = new EffectiveAggregates(
                TotalAssets: effectiveTotalAssets,
                TotalLiquidAssets: effectiveLiquidAssets,
                TotalRetirementSavings: aggregated.TotalRetirementSavings, // no safe fallback unless you add a field
                TotalDebt: effectiveTotalDebtBalance,
                WeightedDebtRate: effectiveWeightedDebtRate,
                TotalMonthlyDebtPayments: effectiveMonthlyDebtPayments
            );

            return new List<SubScore>
            {
                new SubScore("Income Stability", await CalculateIncomeScore(client), 0.40m),
                new SubScore("Savings Health", CalculateSavingsScore(eff), 0.20m),
                new SubScore("Debt Load", CalculateDebtScore(client, eff), 0.20m),
                new SubScore("Emergency Fund", CalculateEmergencyFundScore(client, eff), 0.10m),
                new SubScore("Retirement Readiness", CalculateRetirementScore(client, eff), 0.10m)
            };
        }

        /// <summary>
        /// Evaluates income by ranking the user within their demographic percentile curve.
        /// </summary>
        private async Task<decimal> CalculateIncomeScore(ClientProfile client)
        {
            if (client.MonthlyIncome <= 0) return 0m;

            var benchmark = await _benchmarkProvider.GetIncomeBenchmarkAsync(
                client.Age,
                client.LocationState ?? "NY",
                client.Gender
            );

            if (benchmark == null)
            {
                decimal fallbackMonthlyMedian = 100000m / 12m;
                return Math.Min(100m, (client.MonthlyIncome / fallbackMonthlyMedian) * 50m);
            }

            decimal annualIncome = client.MonthlyIncome * 12m;

            if (annualIncome >= benchmark.P95) return 100m;
            if (annualIncome >= benchmark.P75) return 85m + ((annualIncome - benchmark.P75) / (benchmark.P95 - benchmark.P75) * 15m);
            if (annualIncome >= benchmark.MedianIncome) return 60m + ((annualIncome - benchmark.MedianIncome) / (benchmark.P75 - benchmark.MedianIncome) * 25m);
            if (annualIncome >= benchmark.P25) return 30m + ((annualIncome - benchmark.P25) / (benchmark.MedianIncome - benchmark.P25) * 30m);

            return (annualIncome / benchmark.P25) * 30m;
        }

        private decimal CalculateSavingsScore(EffectiveAggregates aggregated)
        {
            var savings = aggregated.TotalAssets;
            var debt = aggregated.TotalDebt;

            // If no debt, savings health is maxed (solvency strong)
            if (debt <= 0m) return 100m;

            if (savings <= 0m) return 0m;

            return Math.Min(100m, (savings / debt) * 100m);
        }

        private decimal CalculateDebtScore(ClientProfile client, EffectiveAggregates aggregated)
        {
            decimal totalOutflow = client.MonthlyExpense + aggregated.TotalMonthlyDebtPayments;
            decimal debtRatio = totalOutflow / Math.Max(client.MonthlyIncome, 1m);

            // 0.60 is the threshold; higher ratio -> lower score
            decimal cashFlowScore = Math.Max(0m, (1m - (debtRatio / 0.6m)) * 100m);

            // APR penalty uses FRACTIONAL rates (0.07 = 7%)
            // Threshold at 7% (0.07)
            decimal interestPenalty = 1.0m;
            if (aggregated.WeightedDebtRate > 0.07m)
            {
                decimal penalty = aggregated.WeightedDebtRate - 0.07m;
                interestPenalty = Math.Max(0.2m, 1.0m - (penalty * 5.0m)); // 0.01 over -> 5% penalty
            }

            return cashFlowScore * interestPenalty;
        }

        private decimal CalculateEmergencyFundScore(ClientProfile client, EffectiveAggregates aggregated)
        {
            decimal target = client.MonthlyExpense * 6m;
            if (target <= 0m) return 100m;

            return Math.Min(100m, (aggregated.TotalLiquidAssets / target) * 100m);
        }

        private decimal CalculateRetirementScore(ClientProfile client, EffectiveAggregates aggregated)
        {
            // Fidelity-ish guideline proxy
            decimal ageMultiplier = client.Age < 30 ? 1m : (client.Age - 20) / 5m;
            decimal target = client.MonthlyIncome * 12m * ageMultiplier;

            return Math.Min(100m, (aggregated.TotalRetirementSavings / Math.Max(target, 1m)) * 100m);
        }

        private void ValidateWeights(IEnumerable<SubScore> subScores)
        {
            var sum = subScores.Sum(s => s.Weight);
            if (Math.Abs(sum - 1.0m) > 0.0001m)
                throw new InvalidOperationException($"Score weights must total 1.0. Current sum: {sum}");
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
                    new FinancialItem { Label = "Checking", Amount = monthly * 2m, IsDebt = false },
                    new FinancialItem { Label = "Debt", Amount = monthly * 1m, MonthlyPayment = monthly * 0.1m, InterestRate = 0.07m, IsDebt = true }
                }
            };
        }

        private readonly record struct EffectiveAggregates(
            decimal TotalAssets,
            decimal TotalLiquidAssets,
            decimal TotalRetirementSavings,
            decimal TotalDebt,
            decimal WeightedDebtRate,
            decimal TotalMonthlyDebtPayments
        );
    }
}
