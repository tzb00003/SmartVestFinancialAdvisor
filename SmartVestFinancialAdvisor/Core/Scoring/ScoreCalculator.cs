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

        public async Task<FinancialScore> AggregateScore(ClientProfile client)
        {
            var result = await Calculate(client);

            return new FinancialScore
            {
                Total = result.TotalScore,
                SubScores = result.SubScores
            };
        }

        // ================= BUILD SUBSCORES =================

        private async Task<List<SubScore>> BuildSubScores(ClientProfile client)
        {
            var items = client.Items ?? new List<FinancialItem>();
            var aggregated = _aggregationService.Aggregate(items);

            // ---- Safe fallbacks when item detail is incomplete ----
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

            var eff = new EffectiveAggregates(
                TotalAssets: effectiveTotalAssets,
                TotalLiquidAssets: effectiveLiquidAssets,
                TotalRetirementSavings: aggregated.TotalRetirementSavings,
                TotalDebt: effectiveTotalDebtBalance,
                WeightedDebtRate: effectiveWeightedDebtRate,
                TotalMonthlyDebtPayments: effectiveMonthlyDebtPayments
            );

            return new List<SubScore>
            {
                new SubScore("Income Stability", await CalculateIncomeScore(client), 0.40m),
                new SubScore("Savings Health", CalculateSavingsScore(eff), 0.20m),
                new SubScore("Debt Load", CalculateDebtLoadScore(client, eff), 0.20m),
                new SubScore("Emergency Fund", CalculateEmergencyFundScore(client, eff), 0.10m),
                new SubScore("Retirement Readiness", CalculateRetirementScore(client, eff), 0.10m)
            };
        }

        // ================= INCOME =================

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
            if (annualIncome >= benchmark.P75)
                return 85m + ((annualIncome - benchmark.P75) / (benchmark.P95 - benchmark.P75) * 15m);
            if (annualIncome >= benchmark.MedianIncome)
                return 60m + ((annualIncome - benchmark.MedianIncome) / (benchmark.P75 - benchmark.MedianIncome) * 25m);
            if (annualIncome >= benchmark.P25)
                return 30m + ((annualIncome - benchmark.P25) / (benchmark.MedianIncome - benchmark.P25) * 30m);

            return (annualIncome / benchmark.P25) * 30m;
        }

        // ================= SAVINGS =================

        private decimal CalculateSavingsScore(EffectiveAggregates aggregated)
        {
            if (aggregated.TotalDebt <= 0m)
                return 100m;

            if (aggregated.TotalAssets <= 0m)
                return 0m;

            return Math.Min(100m, (aggregated.TotalAssets / aggregated.TotalDebt) * 100m);
        }

        // ================= DEBT LOAD (FIXED) =================

        private decimal CalculateDebtLoadScore(ClientProfile client, EffectiveAggregates aggregated)
        {
            if (client.MonthlyIncome <= 0m)
                return 0m;

            // ---- Expense pressure (cannot zero score alone) ----
            decimal expenseRatio = client.MonthlyExpense / client.MonthlyIncome;
            decimal baseExpenseScore = Math.Max(0m, (1m - expenseRatio) * 100m);

            decimal expensePenalty =
                expenseRatio <= 0.60m
                    ? 0m
                    : Math.Min(0.40m, expenseRatio - 0.60m);

            decimal score = baseExpenseScore * (1m - expensePenalty);

            // ---- Debt payment pressure ----
            if (aggregated.TotalMonthlyDebtPayments > 0m)
            {
                decimal debtPaymentRatio = aggregated.TotalMonthlyDebtPayments / client.MonthlyIncome;
                decimal debtPenalty = Math.Min(0.60m, debtPaymentRatio * 2.5m);
                score *= (1m - debtPenalty);
            }

            // ---- APR penalty (only if debt exists, >6%) ----
            if (aggregated.TotalDebt > 0m && aggregated.WeightedDebtRate >= 0.06m)
            {
                decimal aprPenalty = Math.Min(0.30m, (aggregated.WeightedDebtRate - 0.06m) * 4.0m);
                score *= (1m - aprPenalty);
            }

            return Math.Clamp(score, 0m, 100m);
        }

        // ================= EMERGENCY FUND =================

        private decimal CalculateEmergencyFundScore(ClientProfile client, EffectiveAggregates aggregated)
        {
            decimal target = client.MonthlyExpense * 6m;
            if (target <= 0m) return 100m;

            return Math.Min(100m, (aggregated.TotalLiquidAssets / target) * 100m);
        }

        // ================= RETIREMENT =================

        private decimal CalculateRetirementScore(ClientProfile client, EffectiveAggregates aggregated)
        {
            decimal ageMultiplier = client.Age < 30 ? 1m : (client.Age - 20) / 5m;
            decimal target = client.MonthlyIncome * 12m * ageMultiplier;

            return Math.Min(100m, (aggregated.TotalRetirementSavings / Math.Max(target, 1m)) * 100m);
        }

        // ================= PEER AVATAR (RESTORED & FIXED) =================

        /// <summary>
        /// Generates a realistic peer profile at a given income percentile.
        /// Used for benchmarking and category comparisons.
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

            decimal monthlyIncome = annualIncome / 12m;

            // Conservative but realistic assumptions
            decimal monthlyExpense = monthlyIncome * 0.45m;
            decimal emergencyFund = monthlyExpense * 3m;
            decimal retirementSavings = annualIncome * 0.5m;

            return new ClientProfile
            {
                MonthlyIncome = monthlyIncome,
                MonthlyExpense = monthlyExpense,
                Age = (benchmark.AgeRangeMin + benchmark.AgeRangeMax) / 2,
                LocationState = benchmark.State,
                Gender = benchmark.Gender,

                Items = new List<FinancialItem>
                {
                    new FinancialItem
                    {
                        Label = "Emergency Fund",
                        Amount = emergencyFund,
                        IsDebt = false
                    },
                    new FinancialItem
                    {
                        Label = "Retirement Savings",
                        Amount = retirementSavings,
                        IsDebt = false,
                        IsRetirement = true
                    },
                    new FinancialItem
                    {
                        Label = "Typical Auto/Student Loan",
                        Amount = monthlyIncome * 6m,
                        MonthlyPayment = monthlyIncome * 0.08m,
                        InterestRate = 0.05m, // NOT high-interest
                        IsDebt = true
                    }
                }
            };
        }

        // ================= UTIL =================

        private void ValidateWeights(IEnumerable<SubScore> subScores)
        {
            var sum = subScores.Sum(s => s.Weight);
            if (Math.Abs(sum - 1.0m) > 0.0001m)
                throw new InvalidOperationException($"Score weights must total 1.0. Current sum: {sum}");
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