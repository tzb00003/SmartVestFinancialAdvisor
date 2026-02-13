using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Core.Benchmarks;
using SmartVestFinancialAdvisor.Infrastructure.Benchmarks;

namespace SmartVestFinancialAdvisor.Infrastructure.Census
{
    public class CensusIngestionAgent
    {
        private readonly CensusApiClient _apiClient;
        private readonly SqliteBenchmarkProvider _benchmarkProvider;
        private readonly WorksCitedManager _citationManager;

        // Multipliers for the "Pure Individual" Statistical Curve
        private const decimal MultiplierP25 = 0.65m;
        private const decimal MultiplierP75 = 1.65m;
        private const decimal MultiplierP95 = 3.10m;

        // Variable Map for B20018 (Full-Time Year-Round Workers)
        private static readonly Dictionary<string, (int Min, int Max, string Gender)> DemographicVariables = new()
        {
            { "B20018_002E", (18, 24, "Male") },
            { "B20018_003E", (25, 44, "Male") },
            { "B20018_004E", (45, 64, "Male") },
            { "B20018_007E", (18, 24, "Female") },
            { "B20018_008E", (25, 44, "Female") },
            { "B20018_009E", (45, 64, "Female") }
        };

        private static readonly Dictionary<string, string> FipsToState = new() { /* ... Same FIPS mapping as before ... */ };

        public CensusIngestionAgent(string dbPath, string rootPath)
        {
            _apiClient = new CensusApiClient();
            _benchmarkProvider = new SqliteBenchmarkProvider(dbPath);
            _citationManager = new WorksCitedManager(rootPath);
        }

        public async Task RunIngestionAsync()
        {
            int year = 2022; // Use current available ACS1 year
            Console.WriteLine($"[CensusAgent] Starting ingestion for {year} Individual Earnings (B20018)...");

            // Step 1: Extract
            string jsonResponse = await _apiClient.FetchWorkingAgeEarningsAsync(year);

            if (string.IsNullOrEmpty(jsonResponse)) return;

            // Step 2: Transform (Normalization + Percentile Calculation)
            var benchmarks = ParseCensusResponse(jsonResponse, year);

            // Step 3: Load
            await _benchmarkProvider.BatchInsertBenchmarksAsync(benchmarks);

            // Step 4: Citations
            await _citationManager.AddCitationAsync(
                "Median Earnings for Full-Time Year-Round Workers (B20018)",
                "U.S. Census Bureau",
                year.ToString(),
                $"https://api.census.gov/data/{year}/acs/acs1",
                DateTime.Now.ToString("yyyy-MM-dd")
            );

            Console.WriteLine($"[CensusAgent] Successfully ingested {benchmarks.Count} demographic benchmarks.");
        }

        private List<IncomeBenchmark> ParseCensusResponse(string json, int year)
        {
            var benchmarks = new List<IncomeBenchmark>();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.GetArrayLength() < 2) return benchmarks;

            var headers = root[0];
            var headerMap = new Dictionary<string, int>();
            for (int i = 0; i < headers.GetArrayLength(); i++)
                headerMap[headers[i].GetString() ?? ""] = i;

            for (int i = 1; i < root.GetArrayLength(); i++)
            {
                var row = root[i];
                string stateCode = row[headerMap["state"]].GetString() ?? "";
                if (!FipsToState.TryGetValue(stateCode, out var stateAbbrev)) continue;

                foreach (var kvp in DemographicVariables)
                {
                    if (!headerMap.ContainsKey(kvp.Key)) continue;

                    string valStr = row[headerMap[kvp.Key]].GetString() ?? "0";
                    if (decimal.TryParse(valStr, out decimal median) && median > 0)
                    {
                        // Use the simplified Object Initializer to avoid constructor argument errors
                        benchmarks.Add(new IncomeBenchmark
                        {
                            AgeRangeMin = kvp.Value.Min,
                            AgeRangeMax = kvp.Value.Max,
                            State = stateAbbrev,
                            MedianIncome = median,
                            Gender = Enum.Parse<Gender>(kvp.Value.Gender),
                            Source = "B20018",
                            Year = year,
                            P25 = median * MultiplierP25,
                            P75 = median * MultiplierP75,
                            P95 = median * MultiplierP95
                        });
                    }
                }
            }
            return benchmarks;
        }
    }
}