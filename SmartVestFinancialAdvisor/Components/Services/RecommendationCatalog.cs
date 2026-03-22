using System;
using System.Collections.Generic;
using System.Linq;
using SmartVestFinancialAdvisor.Components.Models;
using SmartVestFinancialAdvisor.Core.Constraints;

namespace SmartVestFinancialAdvisor.Components.Services
{
    /// <summary>
    /// Thread-safe, in-memory recommendation catalog with dynamic scoring
    /// driven by BuildResult (risk tolerance + category subscores + optional facts).
    /// Register as Singleton or Scoped.
    /// </summary>
    public sealed class RecommendationCatalog : IRecommendationCatalog
    {
        private readonly object _gate = new();
        private readonly List<Recommendation> _all;


        public RecommendationCatalog()
        {
            // Expanded seed set: actions + investable ideas.
            // Score10/Info are recomputed per user in For(...).
            _all = new List<Recommendation>
            {
                // ---------- ACTIONS (high-impact hygiene items) ----------
                //new() {
                //    Type = "Pay Down High-Interest Debt",
                //    Risk = "Very Low",
                //    Score10 = 0,
                //    Info = "Lower expensive debt first to free up cash each month and reduce risk."
                //},
                //new() {
                //    Type = "Build/Top-Up Emergency Fund (HYSA/T-Bills)",
                //    Risk = "Very Low",
                //    Score10 = 0,
                //    Info = "Aim for 3–6 months of expenses in a safe, easy‑to‑access account."
                //},
                //new() {
                //    Type = "Max Employer 401(k) Match",
                //    Risk = "Low",
                //    Score10 = 0,
                //    Info = "Don’t leave free money on the table—contribute enough to get the full match."
                //},
                //new() {
                //    Type = "Increase Retirement Contributions",
                //    Risk = "Low–Moderate",
                //    Score10 = 0,
                //    Info = "Boost your savings rate to stay on track for long‑term goals."
                //},

                // ---------------- CASH & CASH‑LIKE ----------------
                new() {
                    Type = "High-Yield Savings Account (HYSA)",
                    Risk = "Very Low",
                    Score10 = 0,
                    Info = "A safe place for near‑term money that earns more than a regular savings account."
                },
                new() {
                    Type = "Treasury Bills (3–6 Months)",
                    Risk = "Very Low",
                    Score10 = 0,
                    Info = "Short‑term U.S. government bonds—very safe with steady, predictable returns."
                },
                new() {
                    Type = "Certificates of Deposit (6–12 Months)",
                    Risk = "Very Low",
                    Score10 = 0,
                    Info = "Lock in a fixed rate for a set time; penalties may apply for early withdrawal."
                },

                // -------------------- BONDS -----------------------
                new() {
                    Type = "Investment-Grade Bond ETF (Core Aggregate)",
                    Risk = "Low",
                    Score10 = 0,
                    Info = "A mix of high‑quality bonds that helps steady your portfolio during market swings."
                },
                new() {
                    Type = "Treasury Ladder (3–12 Months)",
                    Risk = "Very Low",
                    Score10 = 0,
                    Info = "Bonds maturing at different times, so part of your money frees up regularly."
                },
                new() {
                    Type = "TIPS ETF (Inflation-Protected)",
                    Risk = "Low",
                    Score10 = 0,
                    Info = "Bonds designed to help your money keep up with inflation."
                },
                new() {
                    Type = "Municipal Bond Fund (Taxable Accounts)",
                    Risk = "Low",
                    Score10 = 0,
                    Info = "Can reduce taxes on interest if you’re in a higher tax bracket."
                },

                // ------------------- EQUITIES ---------------------
                new() {
                    Type = "Total U.S. Stock Market Index Fund",
                    Risk = "Moderate–High",
                    Score10 = 0,
                    Info = "Low‑cost exposure to nearly the entire U.S. stock market for long‑term growth."
                },
                new() {
                    Type = "S&P 500 Index Fund",
                    Risk = "Moderate–High",
                    Score10 = 0,
                    Info = "Owns 500 large U.S. companies; a simple, low‑cost core for long‑term investing."
                },
                new() {
                    Type = "Total International Stock Index Fund",
                    Risk = "High",
                    Score10 = 0,
                    Info = "Adds companies outside the U.S. for global diversification."
                },
                new() {
                    Type = "Small-Cap Value Tilt ETF",
                    Risk = "High",
                    Score10 = 0,
                    Info = "Focuses on smaller, value‑oriented companies—higher return potential with more ups and downs."
                },
                new() {
                    Type = "REIT (Diversified)",
                    Risk = "Moderate–High",
                    Score10 = 0,
                    Info = "Invests in real estate businesses that generate income from rent and properties."
                },

                // ---------------- OTHER / HEDGES ------------------
                new() {
                    Type = "Commodities Basket",
                    Risk = "High",
                    Score10 = 0,
                    Info = "A mix of commodities (like energy and metals) that may help during high inflation—but can be volatile."
                }
            };

        }

        public IReadOnlyList<Recommendation> All
        {
            get { lock (_gate) { return _all.ToList(); } } // snapshot
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
        /// Returns a ranked list of recommendations tailored to the user's signals.
        /// The UI will typically call .Take(4) to show Top 4.
        /// </summary>
        public IReadOnlyList<Recommendation> For(BuildResult result)
        {
            if (result is null) return Array.Empty<Recommendation>();

            var sig = AdvisorSignals.From(result);

            // Guardrails: if debt or emergency are weak, prioritize actions/cash.
            bool needsDebtAction = sig.DebtLoadScore < 55m || sig.AverageDebtAPR >= 7.0m;
            bool needsEmergency = sig.EmergencyScore < 80m || sig.EmergencyMonths < 3m;

            // Working copy per user
            var tailored = All.Select(r => new Recommendation
            {
                Type = r.Type,
                Risk = r.Risk,
                Link = r.Link,
                Score10 = 0,  // dynamic
                Info = r.Info
            }).ToList();

            foreach (var rec in tailored)
            {
                decimal s = 0m;
                string why = string.Empty;

                // --- ACTIONS ---
                if (rec.Type.Contains("Pay Down High-Interest Debt", StringComparison.OrdinalIgnoreCase))
                {
                    s = ScoreFromBands(
                        strong: needsDebtAction,
                        moderate: sig.DebtLoadScore < 70m,
                        baseScore: 3m,
                        strongScore: 10m,
                        moderateScore: 7m
                    );
                    if (sig.AverageDebtAPR >= 12m) s = 10m; // extreme APR
                    why = $"Debt load score {sig.DebtLoadScore:N0}/100; avg APR {sig.AverageDebtAPR:N1}%. Reducing high-cost debt improves cash flow and risk capacity.";
                }
                else if (rec.Type.Contains("Build/Top-Up Emergency Fund", StringComparison.OrdinalIgnoreCase))
                {
                    s = ScoreFromBands(
                        strong: needsEmergency,
                        moderate: sig.EmergencyMonths < 6m,
                        baseScore: 4m,
                        strongScore: 10m,
                        moderateScore: 7m
                    );
                    why = $"Emergency fund covers {sig.EmergencyMonths:N1} months (target 3–6). Cash-like reserves increase resilience.";
                }
                else if (rec.Type.Contains("Max Employer 401(k) Match", StringComparison.OrdinalIgnoreCase))
                {
                    s = (sig.HasEmployerMatch ? 9m : 6m);
                    why = sig.HasEmployerMatch
                        ? "Uncaptured employer match is a guaranteed, risk-free return; prioritize contributions to secure full match."
                        : "If employer match exists, capturing it is a priority. Otherwise this remains a solid retirement saving action.";
                }
                else if (rec.Type.Contains("Increase Retirement Contributions", StringComparison.OrdinalIgnoreCase))
                {
                    s = LerpByDeficit(sig.RetirementScore, lowIsStronger: true); // lower score -> higher priority
                    why = $"Retirement readiness score {sig.RetirementScore:N0}/100; increasing savings rate can close the gap.";
                }

                // --- CASH / CASH-LIKE ---
                else if (rec.Type.Contains("High-Yield Savings Account", StringComparison.OrdinalIgnoreCase))
                {
                    s = needsEmergency ? 9m : (sig.RiskTolerance <= 0.40m ? 8m : 5m);
                    why = needsEmergency
                        ? "Boost emergency reserves in HYSA to reach 3–6 months of expenses."
                        : "Maintain liquidity for near-term needs; HYSA offers competitive yield with FDIC coverage if bank-issued.";
                }
                else if (rec.Type.Contains("Treasury Bills", StringComparison.OrdinalIgnoreCase))
                {
                    s = needsEmergency ? 8m : (sig.RiskTolerance <= 0.40m ? 8m : 6m);
                    why = "Short-duration Treasuries provide liquidity and low credit risk; interest is state-tax exempt.";
                }
                else if (rec.Type.Contains("Certificates of Deposit", StringComparison.OrdinalIgnoreCase))
                {
                    s = (sig.EmergencyMonths >= 3m ? 7m : 5m);
                    why = "For funds beyond immediate reserves, CDs can lock in yields; consider laddering to manage liquidity.";
                }

                // --- BONDS ---
                else if (rec.Type.Contains("Investment-Grade Bond ETF", StringComparison.OrdinalIgnoreCase))
                {
                    s = sig.RiskTolerance <= 0.40m ? 9m : (sig.RiskTolerance <= 0.70m ? 7m : 5m);
                    why = "Core IG bonds provide diversification and dampen equity volatility; align with conservative to balanced risk.";
                }
                else if (rec.Type.Contains("Treasury Ladder", StringComparison.OrdinalIgnoreCase))
                {
                    s = (sig.EmergencyMonths >= 3m ? 8m : 6m);
                    why = "Laddering 3–12 month Treasuries balances yield and rolling liquidity.";
                }
                else if (rec.Type.Contains("TIPS ETF", StringComparison.OrdinalIgnoreCase))
                {
                    s = sig.InflationConcern ? 8m : 6m;
                    why = "Inflation-protected securities help preserve real purchasing power.";
                }
                else if (rec.Type.Contains("Municipal Bond Fund", StringComparison.OrdinalIgnoreCase))
                {
                    s = sig.IsHighTaxBracket ? 8m : 5m;
                    why = sig.IsHighTaxBracket
                        ? "Tax-exempt income can be attractive in higher marginal tax brackets."
                        : "Consider munis primarily when in a higher marginal tax bracket.";
                }

                // --- EQUITIES ---
                else if (rec.Type.Contains("Total U.S. Stock Market", StringComparison.OrdinalIgnoreCase))
                {
                    s = sig.RiskTolerance switch
                    {
                        <= 0.40m => 5m,
                        <= 0.70m => 8m,
                        _ => 9m
                    };
                    if (needsEmergency || needsDebtAction) s = Math.Max(0m, s - 2m);
                    why = "Broad, low-cost equity exposure; adjust weight to your risk tolerance and liquidity needs.";
                }
                else if (rec.Type.Contains("S&P 500 Index", StringComparison.OrdinalIgnoreCase))
                {
                    s = sig.RiskTolerance switch
                    {
                        <= 0.40m => 5m,
                        <= 0.70m => 7m,
                        _ => 9m
                    };
                    if (needsEmergency || needsDebtAction) s = Math.Max(0m, s - 2m);
                    why = "Large-cap U.S. equities; core growth engine for long-term horizons.";
                }
                else if (rec.Type.Contains("Total International", StringComparison.OrdinalIgnoreCase))
                {
                    s = sig.RiskTolerance <= 0.40m ? 3m : (sig.RiskTolerance <= 0.70m ? 6m : 7m);
                    if (needsEmergency || needsDebtAction) s = Math.Max(0m, s - 1m);
                    why = "International diversification adds currency and regional exposure; higher volatility than U.S.-only.";
                }
                else if (rec.Type.Contains("Small-Cap Value Tilt", StringComparison.OrdinalIgnoreCase))
                {
                    s = sig.RiskTolerance >= 0.70m && !needsDebtAction && !needsEmergency ? 7m : 3m;
                    why = "Factor tilt can improve expected returns with higher volatility; consider as a satellite allocation.";
                }
                else if (rec.Type.Contains("REIT", StringComparison.OrdinalIgnoreCase))
                {
                    s = sig.RiskTolerance >= 0.50m && !needsDebtAction ? 6m : 3m;
                    why = "Real estate income; sensitive to rates and cycles; size as a small satellite position.";
                }

                // --- OTHER / HEDGES ---
                else if (rec.Type.Contains("Commodities Basket", StringComparison.OrdinalIgnoreCase))
                {
                    s = sig.RiskTolerance >= 0.70m && !needsDebtAction ? 6m : 3m;
                    why = "Potential inflation hedge with higher volatility and tracking considerations.";
                }

                // Normalize and assign
                s = Math.Clamp(s, 0m, 10m);
                rec.Score10 = (int)Math.Round(s, MidpointRounding.AwayFromZero);
                rec.Info = why;
            }

            // Risk-aligned filtering
            var filtered = tailored.Where(r =>
            {
                if (sig.RiskTolerance <= 0.40m)
                {
                    // Conservative: emphasize Very Low/Low and essential actions
                    return r.Risk.Contains("Very Low", StringComparison.OrdinalIgnoreCase)
                           || r.Risk.Contains("Low", StringComparison.OrdinalIgnoreCase)
                           || r.Type.StartsWith("Pay Down", StringComparison.OrdinalIgnoreCase)
                           || r.Type.StartsWith("Build/Top-Up Emergency", StringComparison.OrdinalIgnoreCase)
                           || r.Type.StartsWith("Max Employer", StringComparison.OrdinalIgnoreCase)
                           || r.Type.StartsWith("Increase Retirement", StringComparison.OrdinalIgnoreCase);
                }
                else if (sig.RiskTolerance >= 0.70m)
                {
                    // Aggressive: include Moderate–High/High + essential actions if needed
                    return r.Risk.Contains("Moderate", StringComparison.OrdinalIgnoreCase)
                           || r.Risk.Contains("High", StringComparison.OrdinalIgnoreCase)
                           || r.Risk.Contains("Moderate–High", StringComparison.OrdinalIgnoreCase)
                           || r.Type.StartsWith("Pay Down", StringComparison.OrdinalIgnoreCase)
                           || r.Type.StartsWith("Build/Top-Up Emergency", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    // Balanced: broad mix; exclude niche high-vol unless score is strong
                    if (r.Risk.Contains("High", StringComparison.OrdinalIgnoreCase) || r.Risk.Contains("Moderate–High", StringComparison.OrdinalIgnoreCase))
                        return r.Score10 >= 6;
                    return true;
                }
            });

            // Rank and return positives only (UI will Take(4))
            var ranked = filtered
                .Where(r => r.Score10 > 0)
                .OrderByDescending(r => r.Score10)
                .ThenBy(r => r.Risk, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return ranked;
        }

        // ----------------- helpers -----------------

        private static int LerpByDeficit(decimal score100, bool lowIsStronger)
        {
            // Map 0..100 -> 10..1 (if lowIsStronger), else 1..10
            var t = Math.Clamp(score100 / 100m, 0m, 1m);
            var v = lowIsStronger ? (10m - 9m * t) : (1m + 9m * t);
            return (int)Math.Round(v, MidpointRounding.AwayFromZero);
        }

        private static decimal ScoreFromBands(bool strong, bool moderate, decimal baseScore, decimal strongScore, decimal moderateScore)
            => strong ? strongScore : (moderate ? moderateScore : baseScore);

        // ---------- Signals extractor (SubScores + optional Facts) ----------

        private sealed record AdvisorSignals(
            decimal RiskTolerance,
            decimal DebtLoadScore,
            decimal EmergencyScore,
            decimal EmergencyMonths,
            decimal RetirementScore,
            decimal AverageDebtAPR,
            bool HasEmployerMatch,
            bool InflationConcern,
            bool IsHighTaxBracket
        )
        {
            public static AdvisorSignals From(BuildResult r)
            {
                var rt = (decimal)(r.Constraints?.RiskTolerance ?? 0.5m);

                // Category scores from FinancialScore.SubScores
                decimal debtLoadScore = GetCategoryScoreFromSubScores(r.Score?.SubScores, "Debt Load") ?? 50m;
                decimal emergencyScore = GetCategoryScoreFromSubScores(r.Score?.SubScores, "Emergency Fund") ?? 50m;
                decimal retirementScore = GetCategoryScoreFromSubScores(r.Score?.SubScores, "Retirement Readiness") ?? 50m;

                // Facts (raw numbers) — optional, falls back safely if null
                var facts = (r as dynamic)?.Facts as IDictionary<string, object?>; // tolerate if Facts not yet added

                decimal emergencyMonths = GetDecimal(facts, "EmergencyMonths")
                                          ?? ComputeEmergencyMonths(facts)
                                          ?? 0m;

                decimal avgApr = GetDecimal(facts, "AverageDebtAPR") ?? 7.0m;

                bool hasMatch = GetBool(facts, "HasEmployerMatch") ?? false;
                bool inflationConcern = GetBool(facts, "InflationConcern") ?? false;
                bool highTax = GetBool(facts, "IsHighTaxBracket") ?? false;

                return new AdvisorSignals(
                    RiskTolerance: rt,
                    DebtLoadScore: Clamp0To100(debtLoadScore),
                    EmergencyScore: Clamp0To100(emergencyScore),
                    EmergencyMonths: emergencyMonths,
                    RetirementScore: Clamp0To100(retirementScore),
                    AverageDebtAPR: avgApr,
                    HasEmployerMatch: hasMatch,
                    InflationConcern: inflationConcern,
                    IsHighTaxBracket: highTax
                );
            }

            private static decimal Clamp0To100(decimal v) => Math.Min(100m, Math.Max(0m, v));

            /// <summary>
            /// Robustly extracts a category score (0..100) from FinancialScore.SubScores,
            /// tolerating different SubScore property names: Name/Area, Score/Score100/RawScore,
            /// or (last resort) WeightedScore divided by Weight when available.
            /// </summary>
            private static decimal? GetCategoryScoreFromSubScores(IReadOnlyList<object>? subScores, string categoryName)
            {
                if (subScores is null) return null;

                foreach (var item in subScores)
                {
                    var t = item.GetType();

                    var nameProp = t.GetProperty("Name") ?? t.GetProperty("Area");
                    var name = nameProp?.GetValue(item) as string;
                    if (string.IsNullOrWhiteSpace(name) ||
                        !string.Equals(name, categoryName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Prefer unweighted score-like fields
                    var scoreProp = t.GetProperty("Score100")
                                 ?? t.GetProperty("Score")
                                 ?? t.GetProperty("RawScore")
                                 ?? t.GetProperty("Value");
                    if (scoreProp is not null)
                    {
                        var raw = scoreProp.GetValue(item);
                        if (ToDecimal(raw) is decimal v) return v;
                    }

                    // Fallback: compute from WeightedScore / Weight when present
                    var wScoreProp = t.GetProperty("WeightedScore");
                    var weightProp = t.GetProperty("Weight");

                    if (wScoreProp is not null && weightProp is not null)
                    {
                        var wRaw = ToDecimal(wScoreProp.GetValue(item));
                        var w = ToDecimal(weightProp.GetValue(item));
                        if (wRaw is decimal ws && w is decimal wt && wt > 0m)
                        {
                            var est = ws / wt; // approximate back to 0..100
                            return Math.Min(100m, Math.Max(0m, est));
                        }
                    }
                }

                return null;
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

            // ---- Facts helpers (safe if Facts is null or missing keys) ----

            private static decimal? GetDecimal(IDictionary<string, object?>? facts, string key)
                => facts is not null && facts.TryGetValue(key, out var v) ? ToDecimal(v) : null;

            private static bool? GetBool(IDictionary<string, object?>? facts, string key)
            {
                if (facts is null || !facts.TryGetValue(key, out var v) || v is null) return null;
                return v switch
                {
                    bool b => b,
                    string s when bool.TryParse(s, out var b2) => b2,
                    int i => i != 0,
                    long l => l != 0,
                    _ => (bool?)null
                };
            }

            private static decimal? ComputeEmergencyMonths(IDictionary<string, object?>? facts)
            {
                var cash = GetDecimal(facts, "Cash") ?? GetDecimal(facts, "LiquidAssets");
                var monthly = GetDecimal(facts, "MonthlyExpenses") ?? GetDecimal(facts, "Expenses");
                if (cash is null || monthly is null || monthly <= 0m) return null;
                return cash / monthly;
            }
        }
    }
}
