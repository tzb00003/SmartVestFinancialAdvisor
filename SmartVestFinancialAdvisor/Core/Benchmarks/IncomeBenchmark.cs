namespace SmartVestFinancialAdvisor.Core.Benchmarks
{
    public enum Gender
    {
        Male,
        Female,
        Other
    }

    /// <summary>
    /// Represents statistical income data for a specific demographic group.
    /// Immutable record for thread-safety and simplicity.
    /// </summary>
    public record IncomeBenchmark(
        int AgeRangeMin,
        int AgeRangeMax,
        string State,
        decimal MedianIncome,
        decimal AverageIncome,
        Gender? Gender = null,
        string Source = "Seed",
        int Year = 0
    );
}
