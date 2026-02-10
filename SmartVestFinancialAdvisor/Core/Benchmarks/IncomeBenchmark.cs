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
        int Year = 0,
        decimal? P10 = null,
        decimal? P25 = null,
        decimal? P75 = null,
        decimal? P90 = null,
        decimal? P95 = null,
        decimal? P99 = null,
        decimal? P99_9 = null
    );
}
