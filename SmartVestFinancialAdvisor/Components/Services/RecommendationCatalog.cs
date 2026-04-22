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
    /// Rules:
    /// - Uses user's risk tolerance to weight product suitability
    /// - Blocks investing recommendations during high debt stress / negative cash flow
    /// - Returns only Top 4 with Score10 > 5
    /// - If none > 5, returns ONLY HYSA
    /// - Explains "why" in Recommendation.Info
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
                    Score10 = 0,
                    Info = "Prioritize paying down high-interest debt to improve cash flow and reduce financial risk."
                },
                new()
                {
                    Type = "Build/Top-Up Emergency Fund",
                    Risk = "Action",
                    Score10 = 0,
                    Info = "Build 3–6 months of expenses in liquid reserves to protect against income shocks."
                },
                new()
                {
                    Type = "Max Employer 401(k) Match",
                    Risk = "Action",
                    Score10 = 0,
                    Info = "If available, contribute enough to capture the full employer match (a high-value benefit)."
                },
                new()
                {
                    Type = "Increase Retirement Contributions",
                    Risk = "Action",
                    Score10 = 0,
                    Info = "Increase retirement contributions to improve long-term readiness."
                },

                // ---------------- CASH & CASH‑LIKE ----------------
                new()
                {
                    Type = HYSA,
                    Risk = "Very Low",
                    Score10 = 0,
                    Info = "Good for emergency funds and short-term goals. Typically FDIC insured (if bank-issued)."
                },
                new()
                {
                    Type = "Treasury Bills (3–6 Months)",
                    Risk = "Very Low",
                    Score10 = 0,
                    Info = "Short-term U.S. Treasuries are a low-risk way to park cash for a few months."
                },
                new()
                {
                    Type = "Certificates of Deposit (6–12 Months)",
                    Risk = "Very Low",
                    Score10 = 0,
                    Info = "Locks in a guaranteed rate for a set term; early withdrawal may incur penalties."
                },

                // -------------------- BONDS -----------------------
                new()
                {
                    Type = "Investment-Grade Bond ETF (Core Aggregate)",
                    Risk = "Low",
                    Score10 = 0,
                    Info = "Diversified high-quality bonds for income and stability."
                },
                new()
                {
                    Type = "TIPS ETF (Inflation-Protected)",
                    Risk = "Low",
                    Score10 = 0,
                    Info = "Inflation-protected bonds help preserve purchasing power."
                },
                new()
                {
                    Type = "Municipal Bond Fund (Taxable Accounts)",
                    Risk = "Low",
                    Score10 = 0,
                    Info = "Potential tax-advantaged bond income (often best for higher tax brackets)."
                },

                // ------------------- EQUITIES ---------------------
                new()
                {
                    Type = "Total U.S. Stock Market Index Fund",
                    Risk = "Moderate–High",
                    Score10 = 0,
                    Info = "Broad U.S. equity exposure for long-term growth."
                },
                new()
                {
                    Type = "S&P 500 Index Fund",
                    Risk = "Moderate–High",
                    Score10 = 0,
                    Info = "Tracks large U.S. companies; common long-term core holding."
                },
                new()
                {
                    Type = "Total International Stock Index Fund",
                    Risk = "High",
                    Score10 = 0,
                    Info = "Diversifies beyond the U.S.; higher volatility than U.S.-only."
                },
                new()
                {
                    Type = "Small-Cap Value Tilt ETF",
                    Risk = "High",
                    Score10 = 0,
                    Info = "Higher-volatility factor tilt; best as a smaller satellite allocation."
                },
                new()
                {
                    Type = "REIT (Diversified)",
                    Risk = "Moderate–High",
                    Score10 = 0,
                    Info = "Real estate exposure for diversification; rate- and cycle-sensitive."
                },

                // ---------------- OTHER / HEDGES ------------------
                new()
                {
                    Type = "Commodities Basket",
                    Risk = "High",
                    Score10 = 0,
                    Info = "Potential inflation hedge with high volatility and tracking considerations."
                }
            };
        }

        public IReadOnlyList<Recommendation> All
        {
            get { lock (_gate) { return _all.ToList(); } }
        }

        public void Add(Recommendation item)
        {
            if (item is null) return;
            lock (_gate) { _all.Add(item); }
        }

        public bool Remove(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return false;
            lock (_gate)
            {
                return _all.RemoveAll(r => string.Equals(r.Type, type, StringComparison.OrdinalIgnoreCase)) > 0;
            }
        }

        /// <summary>
        /// Returns ONLY:
        /// - Top 4 recommendations where Score10 > 5
        /// - If none are > 5: ONLY HYSA
        /// Also blocks investing when debt stress + negative cash flow.
        /// </summary>
        public IReadOnlyList<Recommendation> For(BuildResult result)
        {
            if (result is null) return Array.Empty<Recommendation>();

            var sig = AdvisorSignals.From(result);

            // KEY FIX: Only recommend debt payoff when user actually has debt.
            bool needsDebtAction = sig.HasDebt && (sig.DebtLoadScore < 55m || sig.AverageDebtAprPercent >= 7.0m);

            bool needsEmergency = sig.EmergencyScore < 80m || sig.EmergencyMonths < 3m;

            // Block investing only when debt exists and stress is real
            bool investingBlocked =
                sig.HasDebt &&
                (
                    (sig.NetCashFlow <= 0m && sig.AverageDebtAprPercent >= 7.0m)
                    || (sig.DebtLoadScore < 45m && sig.AverageDebtAprPercent >= 10.0m)
                );

            // Working copy
            var tailored = All.Select(r => new Recommendation
            {
                Type = r.Type,
                Risk = r.Risk,
                Link = r.Link,
                Score10 = 0,
                Info = r.Info
            }).ToList();

            foreach (var rec in tailored)
            {
                decimal score = 0m;
                var why = new List<string>();

                // ============ ACTIONS ============
                if (IsType(rec, "Pay Down High-Interest Debt"))
                {
                    if (!sig.HasDebt)
                    {
                        score = 0m;
                        why.Add("You currently have no debt, so this action is not applicable.");
                    }
                    else
                    {
                        var apr = sig.AverageDebtAprPercent;

                        // --- APR-based priority tiers ---
                        if (apr >= 6.0m)
                        {
                            // High-interest debt → can be a top recommendation
                            score = ScoreFromBands(
                                strong: sig.DebtLoadScore < 55m || sig.NetCashFlow <= 0m,
                                moderate: sig.DebtLoadScore < 70m,
                                baseScore: 4m,
                                strongScore: 9m,
                                moderateScore: 7m
                            );

                            why.Add($"Your average debt interest rate is {apr:0.0}%, which is considered high.");
                        }
                        else if (apr >= 5.0m)
                        {
                            // Awareness tier → never top priority
                            score = 4m;

                            why.Add($"Your average debt interest rate is {apr:0.0}%.");
                            why.Add("This isn’t urgent, but it’s worth monitoring and avoiding adding more debt.");
                        }
                        else
                        {
                            // Low-interest debt → not a priority
                            score = 2m;

                            why.Add($"Your average debt interest rate is {apr:0.0}%.");
                            why.Add("This is relatively low-interest debt and usually not a top payoff priority.");
                        }

                        // Debt load context (separate from APR)
                        why.Add($"Your debt load score is {sig.DebtLoadScore:0}/100.");
                        if (sig.NetCashFlow <= 0m)
                            why.Add("Your monthly cash flow is tight, which limits flexibility.");
                    }
                }
                else if (IsType(rec, "Build/Top-Up Emergency Fund"))
                {
                    score = ScoreFromBands(
                        strong: needsEmergency,
                        moderate: sig.EmergencyMonths < 6m,
                        baseScore: 4m,
                        strongScore: 10m,
                        moderateScore: 7m);

                    why.Add($"Emergency coverage {sig.EmergencyMonths:0.0} months (target 3–6).");
                }
                else if (IsType(rec, "Max Employer 401(k) Match"))
                {
                    score = sig.HasEmployerMatch ? 9m : 6m;
                    why.Add(sig.HasEmployerMatch
                        ? "Employer match available — high-value priority."
                        : "If your employer offers a match, capturing it is a priority.");
                }
                else if (IsType(rec, "Increase Retirement Contributions"))
                {
                    score = LerpByDeficit(sig.RetirementScore, lowIsStronger: true);
                    why.Add($"Retirement readiness score {sig.RetirementScore:0}/100.");
                }

                // ============ CASH ============
                else if (IsType(rec, HYSA))
                {
                    score = needsEmergency ? 9m : (sig.RiskTolerance <= 0.40m ? 8m : 5m);
                    why.Add(needsEmergency ? "Strengthen emergency reserves." : "Maintain liquidity for near-term needs.");
                }
                else if (Contains(rec, "Treasury Bills"))
                {
                    score = needsEmergency ? 8m : (sig.RiskTolerance <= 0.40m ? 8m : 6m);
                    why.Add("Low-risk short-term option for cash.");
                }
                else if (Contains(rec, "Certificates of Deposit"))
                {
                    score = sig.EmergencyMonths >= 3m ? 7m : 5m;
                    why.Add("Useful for cash beyond immediate reserves (consider laddering).");
                }

                // ============ BONDS ============
                else if (Contains(rec, "Investment-Grade Bond ETF"))
                {
                    score = sig.RiskTolerance <= 0.40m ? 9m : (sig.RiskTolerance <= 0.70m ? 7m : 5m);
                    why.Add("Adds stability and diversification.");
                }
                else if (Contains(rec, "TIPS ETF"))
                {
                    score = sig.InflationConcern ? 8m : 6m;
                    why.Add("Helps protect purchasing power.");
                }
                else if (Contains(rec, "Municipal Bond Fund"))
                {
                    score = sig.IsHighTaxBracket ? 8m : 5m;
                    why.Add(sig.IsHighTaxBracket
                        ? "Tax-advantaged income may help at higher brackets."
                        : "Most useful in higher marginal tax brackets.");
                }

                // ============ EQUITIES ============
                else if (Contains(rec, "Total U.S. Stock Market"))
                {
                    score = sig.RiskTolerance switch
                    {
                        <= 0.40m => 5m,
                        <= 0.70m => 8m,
                        _ => 9m
                    };
                    why.Add("Broad long-term growth exposure.");
                }
                else if (Contains(rec, "S&P 500"))
                {
                    score = sig.RiskTolerance switch
                    {
                        <= 0.40m => 5m,
                        <= 0.70m => 7m,
                        _ => 9m
                    };
                    why.Add("Large-cap U.S. equities for long-term growth.");
                }
                else if (Contains(rec, "Total International"))
                {
                    score = sig.RiskTolerance <= 0.40m ? 3m : (sig.RiskTolerance <= 0.70m ? 6m : 7m);
                    why.Add("International diversification.");
                }
                else if (Contains(rec, "Small-Cap Value"))
                {
                    score = (sig.RiskTolerance >= 0.70m && !needsDebtAction && !needsEmergency) ? 7m : 3m;
                    why.Add("Higher volatility factor tilt; best as a satellite.");
                }
                else if (Contains(rec, "REIT"))
                {
                    score = (sig.RiskTolerance >= 0.50m && !needsDebtAction) ? 6m : 3m;
                    why.Add("Real estate diversification; rate-sensitive.");
                }

                // ============ OTHER ============
                else if (Contains(rec, "Commodities Basket"))
                {
                    score = (sig.RiskTolerance >= 0.70m && !needsDebtAction) ? 6m : 3m;
                    why.Add("Potential inflation hedge with higher volatility.");
                }

                // Risk-match factor for non-actions
                if (!IsAction(rec))
                {
                    var match = RiskMatch(sig.RiskTolerance, rec.Risk);
                    score *= match;

                    why.Add($"Risk preference: {sig.RiskLevelLabel}.");
                    if (match < 0.75m) why.Add("Lowered because it’s not a strong match to your risk level.");
                }

                // If investing is blocked, suppress investments entirely
                if (investingBlocked && IsInvestment(rec))
                {
                    score = 0m;
                }

                // If debt/emergency needs exist, de-prioritize high-volatility assets
                if ((needsDebtAction || needsEmergency) && IsHighVolInvestment(rec))
                {
                    score = Math.Max(0m, score - 2m);
                }

                score = Math.Clamp(score, 0m, 10m);
                rec.Score10 = (int)Math.Round(score, MidpointRounding.AwayFromZero);

                rec.Info = why.Count > 0
                    ? string.Join(" ", why.Select(x => x.EndsWith(".") ? x : x + "."))
                    : "Recommended based on your financial profile.";
            }

            // If investing is blocked: only allow Actions + HYSA
            if (investingBlocked)
            {
                var blocked = tailored
                    .Where(r => IsAction(r) || IsType(r, HYSA))
                    .Where(r => r.Score10 > 5)
                    .OrderByDescending(r => r.Score10)
                    .ThenBy(r => r.Type, StringComparer.OrdinalIgnoreCase)
                    .Take(4)
                    .ToList();

                if (blocked.Count > 0) return blocked;

                return new List<Recommendation> { FallbackHysa(sig, investingBlocked: true) };
            }

            // Order everything by recommendation strength (best first)
            var ordered = tailored
                .OrderByDescending(r => r.Score10)
                .ThenBy(r => r.Type, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Strong recommendations (used for initial screen)
            var strong = ordered.Where(r => r.Score10 > 5).ToList();

            // If nothing is strong at all, return ONLY HYSA
            if (strong.Count == 0)
                return new List<Recommendation> { FallbackHysa(sig, investingBlocked: false) };

            // ✅ IMPORTANT:
            // Return ALL ordered recommendations.
            // UI controls how many are shown (Generate more).
            return ordered;
        }

        // ---------------- Helpers (Part 2 continues...) ----------------
        private static Recommendation FallbackHysa(AdvisorSignals sig, bool investingBlocked)
        {
            var reason = investingBlocked
                ? "We’re prioritizing stability first due to debt/cash-flow pressure—focus on liquidity and reducing risk before investing."
                : "No options scored above 5 right now—starting with a safe, liquid option is best.";

            return new Recommendation
            {
                Type = HYSA,
                Risk = "Very Low",
                Score10 = 6,
                Link = null,
                Info = $"{reason} Emergency coverage: {sig.EmergencyMonths:0.0} months. Risk preference: {sig.RiskLevelLabel}."
            };
        }

        private static bool IsType(Recommendation r, string type)
            => string.Equals(r.Type, type, StringComparison.OrdinalIgnoreCase);

        private static bool Contains(Recommendation r, string text)
            => r.Type?.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsAction(Recommendation r)
            => string.Equals(r.Risk, "Action", StringComparison.OrdinalIgnoreCase);

        private static bool IsInvestment(Recommendation r)
            => !IsAction(r) && !IsCashLike(r);

        private static bool IsCashLike(Recommendation r)
            => r.Risk.Contains("Very Low", StringComparison.OrdinalIgnoreCase);

        private static bool IsHighVolInvestment(Recommendation r)
            => r.Risk.Contains("High", StringComparison.OrdinalIgnoreCase)
               || r.Risk.Contains("Moderate–High", StringComparison.OrdinalIgnoreCase)
               || r.Risk.Contains("Moderate-High", StringComparison.OrdinalIgnoreCase);

        private static decimal RiskMatch(decimal riskTolerance, string riskLabel)
        {
            var product = riskLabel switch
            {
                var x when x.Contains("Very Low", StringComparison.OrdinalIgnoreCase) => 0.20m,
                var x when x.Contains("Low", StringComparison.OrdinalIgnoreCase) => 0.35m,
                var x when x.Contains("Moderate", StringComparison.OrdinalIgnoreCase) && x.Contains("High", StringComparison.OrdinalIgnoreCase) => 0.75m,
                var x when x.Contains("Moderate", StringComparison.OrdinalIgnoreCase) => 0.55m,
                var x when x.Contains("High", StringComparison.OrdinalIgnoreCase) => 0.90m,
                _ => 0.50m
            };

            var dist = Math.Abs(riskTolerance - product);
            var match = 1.0m - (dist / 0.90m);
            return Math.Clamp(match, 0.50m, 1.05m);
        }

        private static int LerpByDeficit(decimal score100, bool lowIsStronger)
        {
            var t = Math.Clamp(score100 / 100m, 0m, 1m);
            var v = lowIsStronger ? (10m - 9m * t) : (1m + 9m * t);
            return (int)Math.Round(v, MidpointRounding.AwayFromZero);
        }

        private static decimal ScoreFromBands(bool strong, bool moderate, decimal baseScore, decimal strongScore, decimal moderateScore)
            => strong ? strongScore : (moderate ? moderateScore : baseScore);

        // ---------- Signals extractor ----------
        private sealed record AdvisorSignals(
            decimal RiskTolerance,
            string RiskLevelLabel,
            decimal DebtLoadScore,
            decimal EmergencyScore,
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
                var rt = r.Constraints?.RiskTolerance ?? 0.50m;
                var riskLabel = rt switch
                {
                    <= 0.40m => "Low",
                    <= 0.70m => "Medium",
                    _ => "High"
                };

                var sub = r.Score?.SubScores;

                decimal debtLoadScore = GetSubScore(sub, "Debt Load") ?? 50m;
                decimal emergencyScore = GetSubScore(sub, "Emergency Fund") ?? 50m;
                decimal retirementScore = GetSubScore(sub, "Retirement Readiness") ?? 50m;

                var facts = r.Facts;

                // Detect whether the user actually has debt
                var totalDebtBalance = GetDecimal(facts, "TotalDebtBalance") ?? 0m;
                var monthlyDebtPayments = GetDecimal(facts, "TotalMonthlyDebtPayments") ?? 0m;
                bool hasDebt = totalDebtBalance > 0m || monthlyDebtPayments > 0m;

                decimal emergencyMonths =
                    GetDecimal(facts, "EmergencyMonths")
                    ?? ComputeEmergencyMonths(facts)
                    ?? 0m;

                // APR may be fraction (0.07) or percent (7). If NO debt, APR = 0.
                decimal aprRaw =
                    hasDebt
                        ? (GetDecimal(facts, "WeightedDebtRate")
                           ?? GetDecimal(facts, "AverageDebtAPR")
                           ?? 7.0m)
                        : 0m;

                decimal aprPercent = aprRaw <= 1.0m ? aprRaw * 100m : aprRaw;

                var monthlyIncome = GetDecimal(facts, "MonthlyIncome") ?? 0m;
                var monthlyExpenses = GetDecimal(facts, "MonthlyExpenses") ?? GetDecimal(facts, "MonthlyExpense") ?? 0m;

                decimal netCashFlow = monthlyIncome - (monthlyExpenses + monthlyDebtPayments);

                bool hasMatch = GetBool(facts, "HasEmployerMatch") ?? false;
                bool inflationConcern = GetBool(facts, "InflationConcern") ?? false;
                bool highTax = GetBool(facts, "IsHighTaxBracket") ?? false;

                return new AdvisorSignals(
                    RiskTolerance: rt,
                    RiskLevelLabel: riskLabel,
                    DebtLoadScore: Clamp0To100(debtLoadScore),
                    EmergencyScore: Clamp0To100(emergencyScore),
                    EmergencyMonths: emergencyMonths,
                    RetirementScore: Clamp0To100(retirementScore),
                    AverageDebtAprPercent: aprPercent,
                    NetCashFlow: netCashFlow,
                    HasEmployerMatch: hasMatch,
                    InflationConcern: inflationConcern,
                    IsHighTaxBracket: highTax,
                    HasDebt: hasDebt
                );
            }

            private static decimal Clamp0To100(decimal v) => Math.Min(100m, Math.Max(0m, v));

            private static decimal? GetSubScore(IReadOnlyList<SubScore>? subScores, string name)
            {
                if (subScores is null) return null;
                return subScores.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))?.RawScore;
            }

            private static decimal? GetDecimal(IDictionary<string, object?>? facts, string key)
                => facts != null && facts.TryGetValue(key, out var v) ? ToDecimal(v) : null;

            private static bool? GetBool(IDictionary<string, object?>? facts, string key)
            {
                if (facts == null || !facts.TryGetValue(key, out var v) || v is null) return null;
                return v switch
                {
                    bool b => b,
                    string s when bool.TryParse(s, out var b2) => b2,
                    int i => i != 0,
                    long l => l != 0,
                    _ => (bool?)null
                };
            }

            private static decimal? ToDecimal(object? raw) => raw switch
            {
                null => (decimal?)null,
                decimal d => d,
                double d => (decimal)d,
                float f => (decimal)f,
                int i => i,
                long l => l,
                string s when decimal.TryParse(s, out var v) => v,
                _ => null
            };

            private static decimal? ComputeEmergencyMonths(IDictionary<string, object?>? facts)
            {
                var cash = GetDecimal(facts, "Cash") ?? GetDecimal(facts, "TotalLiquidAssets");
                var monthly = GetDecimal(facts, "MonthlyExpenses") ?? GetDecimal(facts, "MonthlyExpense");
                if (cash is null || monthly is null || monthly <= 0m) return null;
                return cash.Value / monthly.Value;
            }
        }
    }
}
