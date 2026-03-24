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

        private const decimal MultiplierP25 = 0.65m;
        private const decimal MultiplierP75 = 1.65m;
        private const decimal MultiplierP95 = 3.10m;

        private static readonly Dictionary<string, (int Min, int Max, string Gender)> DemographicVariables = new()
        {
            { "S2001_C01_002E", (18, 80, "Other") },  // Total
            { "S2001_C02_002E", (18, 80, "Male") },   // Male
            { "S2001_C03_002E", (18, 80, "Female") }  // Female
        };

        private static readonly Dictionary<string, string> FipsToState = new()
        {
            { "01", "AL" }, { "02", "AK" }, { "04", "AZ" }, { "05", "AR" }, { "06", "CA" },
            { "08", "CO" }, { "09", "CT" }, { "10", "DE" }, { "11", "DC" }, { "12", "FL" },
            { "13", "GA" }, { "15", "HI" }, { "16", "ID" }, { "17", "IL" }, { "18", "IN" },
            { "19", "IA" }, { "20", "KS" }, { "21", "KY" }, { "22", "LA" }, { "23", "ME" },
            { "24", "MD" }, { "25", "MA" }, { "26", "MI" }, { "27", "MN" }, { "28", "MS" },
            { "29", "MO" }, { "30", "MT" }, { "31", "NE" }, { "32", "NV" }, { "33", "NH" },
            { "34", "NJ" }, { "35", "NM" }, { "36", "NY" }, { "37", "NC" }, { "38", "ND" },
            { "39", "OH" }, { "40", "OK" }, { "41", "OR" }, { "42", "PA" }, { "44", "RI" },
            { "45", "SC" }, { "46", "SD" }, { "47", "TN" }, { "48", "TX" }, { "49", "UT" },
            { "50", "VT" }, { "51", "VA" }, { "53", "WA" }, { "54", "WV" }, { "55", "WI" },
            { "56", "WY" }
        };

        public CensusIngestionAgent(string dbPath, string rootPath)
        {
            _apiClient = new CensusApiClient();
            _benchmarkProvider = new SqliteBenchmarkProvider(dbPath);
        }

        public async Task RunIngestionAsync()
        {
            int year = 2022;
            string vars = "S2001_C01_002E,S2001_C02_002E,S2001_C03_002E";

            string jsonResponse = await _apiClient.FetchDataAsync(year, "acs1", vars);

            if (string.IsNullOrEmpty(jsonResponse)) return;

            var benchmarks = ParseCensusResponse(jsonResponse, year);
            if (benchmarks.Count > 0)
            {
                await _benchmarkProvider.BatchInsertBenchmarksAsync(benchmarks);
                Console.WriteLine($"[CensusAgent] Success! Ingested {benchmarks.Count} benchmarks from S2001.");
            }
        }

        private List<IncomeBenchmark> ParseCensusResponse(string json, int year)
        {
            var benchmarks = new List<IncomeBenchmark>();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var headers = root[0];
            var headerMap = new Dictionary<string, int>();
            for (int i = 0; i < headers.GetArrayLength(); i++)
                headerMap[headers[i].GetString() ?? ""] = i;

            for (int i = 1; i < root.GetArrayLength(); i++)
            {
                var row = root[i];
                string stateCode = row[headerMap["state"]].GetString() ?? "";

                if (FipsToState.TryGetValue(stateCode, out var stateAbbrev))
                {
                    foreach (var kvp in DemographicVariables)
                    {
                        string valStr = row[headerMap[kvp.Key]].GetString() ?? "0";
                        if (decimal.TryParse(valStr, out decimal median) && median > 0)
                        {
                            benchmarks.Add(new IncomeBenchmark
                            {
                                AgeRangeMin = kvp.Value.Min,
                                AgeRangeMax = kvp.Value.Max,
                                State = stateAbbrev,
                                MedianIncome = median,
                                Gender = Enum.Parse<Gender>(kvp.Value.Gender),
                                Source = "S2001",
                                Year = year,
                                P25 = median * MultiplierP25,
                                P75 = median * MultiplierP75,
                                P95 = median * MultiplierP95
                            });
                        }
                    }
                }
            }
            return benchmarks;
        }
    }
}