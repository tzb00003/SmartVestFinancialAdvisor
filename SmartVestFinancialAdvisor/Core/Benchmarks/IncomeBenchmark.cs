namespace SmartVestFinancialAdvisor.Core.Benchmarks
{
    public enum Gender { Male, Female, Other }

    public record IncomeBenchmark
    {
        // Required for Object Initializer { AgeRangeMin = ... }
        public IncomeBenchmark() { }

        // Required for Positional Logic
        public IncomeBenchmark(
            int ageRangeMin, int ageRangeMax, string state,
            decimal medianIncome, Gender? gender, string source,
            int year, decimal p25, decimal p75, decimal p95)
        {
            AgeRangeMin = ageRangeMin;
            AgeRangeMax = ageRangeMax;
            State = state;
            MedianIncome = medianIncome;
            Gender = gender;
            Source = source;
            Year = year;
            P25 = p25;
            P75 = p75;
            P95 = p95;
        }

        public int AgeRangeMin { get; init; }
        public int AgeRangeMax { get; init; }
        public string State { get; init; } = string.Empty;
        public decimal MedianIncome { get; init; }
        public Gender? Gender { get; init; }
        public string Source { get; init; } = "B20018";
        public int Year { get; init; }
        public decimal P25 { get; init; }
        public decimal P75 { get; init; }
        public decimal P95 { get; init; }
    }
}
