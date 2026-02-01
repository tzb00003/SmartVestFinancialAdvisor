using Core.Scoring;
using Xunit;

public class ScoreCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsPositiveScore_ForValidProfile()
    {
        // Arrange
        var profile = new ClientProfile
        {
            MonthlyIncome = 6000m,
            Savings = 15000m,
            MonthlyDebt = 1000m
        };

        var calculator = new ScoreCalculator();

        // Act
        var result = calculator.Calculate(profile);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalScore > 0);
        Assert.NotEmpty(result.SubScores);
    }
}