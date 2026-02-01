using SmartVestFinancialAdvisor.Core.Scoring;

namespace SmartVestFinancialAdvisor.Core.Constraints
{
    public class Builder
    {
        private readonly ScoreCalculator _scoreCalculator;

        public Builder()
        {
            _scoreCalculator = new ScoreCalculator();
        }

        public BuildResult Build(FinancialProfile profile)
        {
            // 1. Convert FinancialProfile to ClientProfile
            var client = new ClientProfile
            {
                MonthlyIncome = profile.MonthlyIncome,
                Savings = profile.Savings,
                MonthlyDebt = profile.MonthlyDebt
            };

            // 2. Calculate financial score
            FinancialScore score = _scoreCalculator.AggregateScore(client);

            // 3. Determine client category
            ClientCategory category = Categories.DetermineCategory(score);

            // 4. Get category definition
            var definition = Categories.GetCategoryDefinition(category);

            // 5. Build portfolio constraints
            var constraints = new PortfolioConstraints
            {
                RiskTolerance = profile.RiskTolerance,
                MaxStockAllocation = definition.DefaultStockAllocation,
                MaxBondAllocation = definition.DefaultBondAllocation,
                MaxCashAllocation = definition.DefaultCashAllocation
            };

            return new BuildResult
            {
                Score = score,
                Category = category,
                Constraints = constraints
            };
        }
    }

    public class BuildResult
    {
        public FinancialScore Score { get; set; }
        public ClientCategory Category { get; set; }
        public PortfolioConstraints Constraints { get; set; }
    }
}