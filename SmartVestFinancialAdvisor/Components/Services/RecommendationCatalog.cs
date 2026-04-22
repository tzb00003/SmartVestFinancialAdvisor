using System;
using System.Collections.Generic;
using System.Linq;
using SmartVestFinancialAdvisor.Components.Models;
using SmartVestFinancialAdvisor.Core.Constraints;
using SmartVestFinancialAdvisor.Core.Scoring;

namespace SmartVestFinancialAdvisor.Components.Services
{
    /// <summary>
    /// In-memory recommendation catalog with dynamic scoring driven by BuildResult.
    ///
    /// Behavior:
    /// - Scores and orders ALL recommendations (0–10)
    /// - Applies urgency (debt, emergency), constraints, and risk alignment
    /// - Blocks investing only under real financial stress
    /// - HYSA used as safe fallback when nothing scores strongly
    /// - UI determines how many results to display
    /// </summary>
    public sealed class RecommendationCatalog : IRecommendationCatalog
    {
        private readonly object _gate = new();
        private readonly List<Recommendation> _all;

        private const string HYSA = "High-Yield Savings Account (HYSA)";

        public RecommendationCatalog()
        {
            _all = new List<Recommendation>
            {
                // ---------------- ACTIONS ----------------
                new()
                {
                    Type = "Pay Down High-Interest Debt",
                    Risk = "Action",
                    Info = "Prioritize paying down high-interest debt to improve cash flow and reduce risk."
                },
                new()
                {
                    Type = "Build/Top-Up Emergency Fund",
                    Risk = "Action",
                    Info = "Build 3–6 months of expenses in liquid reserves."
                },
                new()
                {
                    Type = "Max Employer 401(k) Match",
                    Risk = "Action",
                    Info = "Contribute enough to capture the full employer match."
                },
                new()
                {
                    Type = "Increase Retirement Contributions",
                    Risk = "Action",
                    Info = "Boost retirement savings for long-term readiness."
                },

                // ---------------- CASH ----------------
                new()
                {
                    Type = HYSA,
                    Risk = "Very Low",
                    Info = "Liquid, FDIC-insured savings for short-term needs."
                },
                new()
                {
                    Type = "Treasury Bills (3–6 Months)",
                    Risk = "Very Low",
                    Info = "Low-risk U.S. Treasuries with competitive short-term yields."
                },
                new()
                {
                    Type = "Certificates of Deposit (6–12 Months)",
                    Risk = "Very Low",
                    Info = "Fixed-rate deposits with early withdrawal penalties."
                },

                // ---------------- BONDS ----------------
                new()
                {
                    Type = "Investment-Grade Bond ETF (Core Aggregate)",
                    Risk = "Low",
                    Info = "Diversified high-quality bonds for stability."
                },
                new()
                {
                    Type = "TIPS ETF (Inflation-Protected)",
                    Risk = "Low",
                    Info = "Protects purchasing power from inflation."
                },
                new()
                {
                    Type = "Municipal Bond Fund (Taxable Accounts)",
                    Risk = "Low",
                    Info = "Tax-advantaged bond income."
                },

                // ---------------- EQUITIES ----------------
                new()
                {
                    Type = "Total U.S. Stock Market Index Fund",
                    Risk = "Moderate–High",
                    Info = "Broad U.S. market exposure."
                },
                new()
                {
                    Type = "S&P 500 Index Fund",
                    Risk = "Moderate–High",
                    Info = "Large-cap U.S. companies."
                },
                new()
                {
                    Type = "Total International Stock Index Fund",
                    Risk = "High",
                    Info = "International diversification."
                },
                new()
                {
                    Type = "Small-Cap Value Tilt ETF",
                    Risk = "High",
                    Info = "Higher-volatility factor exposure."
                },
                new()
                {
                    Type = "REIT (Diversified)",
                    Risk = "Moderate–High",
                    Info = "Real estate diversification."
                },

                // ---------------- OTHER ----------------
                new()
                {
                    Type = "Commodities Basket",
                    Risk = "High",
                    Info = "Potential inflation hedge with volatility."
                }
            };
        }

        /// <summary>All static/base recommendations in the catalog.</summary>
        public IReadOnlyList<Recommendation> All
        {
            get { lock (_gate) { return _all.ToList(); } }
        }

        /// <summary>Add a recommendation at runtime.</summary>
        public void Add(Recommendation item)
        {
            if (item is null) return;
            lock (_gate)
            {
                _all.Add(item);
            }
        }

        /// <summary>Remove recommendations by Type (case-insensitive).</summary>
        public bool Remove(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return false;

            lock (_gate)
            {
                return _all.RemoveAll(r =>
                    string.Equals(r.Type, type, StringComparison.OrdinalIgnoreCase)) > 0;
            }
        }

        /// <summary>
        /// Returns an ordered set of recommendations tailored to the BuildResult.
        /// </summary>
        public IReadOnlyList<Recommendation> For(BuildResult result)
        {
            if (result is null) return Array.Empty<Recommendation>();

            var sig = AdvisorSignals.From(result);

            bool severeEmergency = sig.EmergencyMonths < 1m;
            bool needsEmergency = sig.EmergencyMonths < 3m;
            bool needsDebtAction = sig.HasDebt && (sig.DebtLoadScore < 55m || sig.AverageDebtAprPercent >= 7m);

            bool investingBlocked =
                sig.HasDebt &&
                (
                    (sig.NetCashFlow <= 0m && sig.AverageDebtAprPercent >= 7m)
                    || (sig.DebtLoadScore < 45m && sig.AverageDebtAprPercent >= 10m)
                );

            var tailored = All.Select(r => new Recommendation
            {
                Type = r.Type,
                Risk = r.Risk,
                Link = r.Link,
                Info = r.Info,
                Score10 = 0
            }).ToList();

            foreach (var rec in tailored)
            {
                decimal score = 0m;
                var why = new List<string>();

                // ---------- ACTIONS ----------
                if (IsType(rec, "Pay Down High-Interest Debt"))
                {
                    if (!sig.HasDebt) score = 0m;
                    else score = sig.AverageDebtAprPercent >= 10m ? 9m :
                                 sig.AverageDebtAprPercent >= 6m ? 7m : 4m;

                    why.Add($"Average debt APR {sig.AverageDebtAprPercent:0.0}%.");
                }
                else if (IsType(rec, "Build/Top-Up Emergency Fund"))
                {
                    score = severeEmergency ? 10m : needsEmergency ? 8m : 5m;
                    why.Add($"Emergency coverage {sig.EmergencyMonths:0.0} months.");
                }
                else if (IsType(rec, "Max Employer 401(k) Match"))
                {
                    score = sig.HasEmployerMatch ? 9m : 5m;
                    if (severeEmergency) score = Math.Min(score, 7m);
                    why.Add("Capturing employer match.");
                }
                else if (IsType(rec, "Increase Retirement Contributions"))
                {
                    score = Math.Min(
                        LerpByDeficit(sig.RetirementScore, lowIsStronger: true),
                        sig.HasEmployerMatch ? 8m : 9m
                    );

                    if (severeEmergency) score = Math.Min(score, 6m);
                    why.Add($"Retirement score {sig.RetirementScore:0}/100.");
                }

                // ---------- CASH ----------
                else if (IsType(rec, HYSA))
                {
                    score = severeEmergency ? 9m : needsEmergency ? 8m : 6m;
                    why.Add("Maintains liquidity.");
                }
                else if (Contains(rec, "Treasury Bills"))
                {
                    score = needsEmergency ? 8m : 7m;
                }
                else if (Contains(rec, "Certificates of Deposit"))
                {
                    score = sig.EmergencyMonths >= 3m ? 7m : 5m;
                }

                // ---------- BONDS ----------
                else if (Contains(rec, "Bond ETF"))
                {
                    score = sig.RiskTolerance <= 0.40m ? 9m : 7m;
                }
                else if (Contains(rec, "TIPS"))
                {
                    score = sig.InflationConcern ? 8m : 6m;
                }
                else if (Contains(rec, "Municipal"))
                {
                    score = sig.IsHighTaxBracket ? 8m : 5m;
                }

                // ---------- EQUITIES ----------
                else if (Contains(rec, "Stock Market") || Contains(rec, "S&P"))
                {
                    score = sig.RiskTolerance < 0.40m ? 5m :
                            sig.RiskTolerance < 0.70m ? 7m : 9m;
                }
                else
                {
                    score = sig.RiskTolerance >= 0.70m ? 6m : 3m;
                }

                // ---------- RISK MATCH ----------
                if (!IsAction(rec))
                {
                    score *= RiskMatch(sig.RiskTolerance, rec.Risk, IsCashLike(rec));
                }

                // ---------- CONSTRAINTS ----------
                if (investingBlocked && IsInvestment(rec))
                    score = 0m;

                if ((needsEmergency || needsDebtAction) && IsHighVolInvestment(rec))
                    score = Math.Min(score, 4m);

                rec.Score10 = (int)Math.Round(
                    Math.Clamp(score, 0m, 10m),
                    MidpointRounding.AwayFromZero);

                rec.Info = string.Join(" ", why);
            }

            var ordered = tailored
                .OrderByDescending(r => r.Score10)
                .ThenBy(r => r.Type)
                .ToList();

            var strong = ordered.Where(r => r.Score10 > 5).ToList();
            return strong.Count > 0
                ? ordered
                : new[] { FallbackHysa(sig) };
        }

        // -------- HELPER METHODS --------
        private static Recommendation FallbackHysa(AdvisorSignals sig) =>
            new()
            {
                Type = HYSA,
                Risk = "Very Low",
                Score10 = 6,
                Info = $"Safe starting point. Emergency coverage: {sig.EmergencyMonths:0.0} months."
            };

        private static bool IsType(Recommendation r, string t)
            => string.Equals(r.Type, t, StringComparison.OrdinalIgnoreCase);

        private static bool Contains(Recommendation r, string t)
            => r.Type?.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsAction(Recommendation r)
            => r.Risk.Equals("Action", StringComparison.OrdinalIgnoreCase);

        private static bool IsCashLike(Recommendation r)
            => r.Risk.Contains("Very Low", StringComparison.OrdinalIgnoreCase);

        private static bool IsInvestment(Recommendation r)
            => !IsAction(r) && !IsCashLike(r);

        private static bool IsHighVolInvestment(Recommendation r)
            => r.Risk.Contains("High", StringComparison.OrdinalIgnoreCase);

        private static decimal RiskMatch(decimal tol, string risk, bool isCash)
        {
            decimal product = risk switch
            {
                var x when x.Contains("Very Low") => 0.25m,
                var x when x.Contains("Low") => 0.40m,
                var x when x.Contains("Moderate") => 0.65m,
                var x when x.Contains("High") => 0.85m,
                _ => 0.50m
            };

            var match = 1.0m - Math.Abs(tol - product) / 0.95m;
            if (isCash) match = Math.Min(match, 1.0m);
            return Math.Clamp(match, 0.65m, 1.05m);
        }

        private static int LerpByDeficit(decimal score100, bool lowIsStronger)
        {
            var t = Math.Clamp(score100 / 100m, 0m, 1m);
            return (int)Math.Round(
                lowIsStronger ? (10m - 8m * t) : (2m + 8m * t),
                MidpointRounding.AwayFromZero);
        }

        // -------- SIGNALS --------
        private sealed record AdvisorSignals(
            decimal RiskTolerance,
            decimal DebtLoadScore,
            decimal EmergencyMonths,
            decimal RetirementScore,
            decimal AverageDebtAprPercent,
            decimal NetCashFlow,
            bool HasEmployerMatch,
            bool InflationConcern,
            bool IsHighTaxBracket,
            bool HasDebt
        )
        {
            public static AdvisorSignals From(BuildResult r)
            {
                var f = r.Facts;

                decimal emergencyMonths =
                    GetDecimal(f, "EmergencyMonths") ??
                    ComputeEmergencyMonths(f) ?? 0m;

                decimal totalDebt = GetDecimal(f, "TotalDebtBalance") ?? 0m;
                decimal monthlyDebt = GetDecimal(f, "TotalMonthlyDebtPayments") ?? 0m;

                bool hasDebt = totalDebt > 0m || monthlyDebt > 0m;

                decimal aprRaw =
                    hasDebt
                        ? GetDecimal(f, "WeightedDebtRate")
                          ?? GetDecimal(f, "AverageDebtAPR")
                          ?? 7m
                        : 0m;

                decimal apr = aprRaw <= 1m ? aprRaw * 100m : aprRaw;

                decimal income = GetDecimal(f, "MonthlyIncome") ?? 0m;
                decimal expenses = GetDecimal(f, "MonthlyExpenses") ?? 0m;

                return new AdvisorSignals(
                    r.Constraints?.RiskTolerance ?? 0.5m,
                    Clamp(GetSub(r, "Debt Load")),
                    emergencyMonths,
                    Clamp(GetSub(r, "Retirement Readiness")),
                    apr,
                    income - (expenses + monthlyDebt),
                    GetBool(f, "HasEmployerMatch") ?? false,
                    GetBool(f, "InflationConcern") ?? false,
                    GetBool(f, "IsHighTaxBracket") ?? false,
                    hasDebt
                );
            }

            private static decimal Clamp(decimal? v) =>
                Math.Clamp(v ?? 50m, 0m, 100m);

            private static decimal? GetSub(BuildResult r, string name) =>
                r.FinancialScore?.SubScores?
                    .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    ?.RawScore;

            private static decimal? GetDecimal(IDictionary<string, object?>? f, string k) =>
                f != null && f.TryGetValue(k, out var v) ? ToDecimal(v) : null;

            private static bool? GetBool(IDictionary<string, object?>? f, string k) =>
                f != null && f.TryGetValue(k, out var v)
                    ? v switch
                    {
                        bool b => b,
                        string s when bool.TryParse(s, out var b) => b,
                        int i => i != 0,
                        _ => null
                    }
                    : null;

            private static decimal? ToDecimal(object? o) =>
                o switch
                {
                    decimal d => d,
                    double d => (decimal)d,
                    float f => (decimal)f,
                    int i => i,
                    long l => l,
                    string s when decimal.TryParse(s, out var v) => v,
                    _ => null
                };

            private static decimal? ComputeEmergencyMonths(IDictionary<string, object?>? f)
            {
                var cash = GetDecimal(f, "Cash") ?? GetDecimal(f, "TotalLiquidAssets");
                var monthly = GetDecimal(f, "MonthlyExpenses");
                return cash is null || monthly is null || monthly <= 0
                    ? null
                    : cash / monthly;
            }
        }
    }
}
