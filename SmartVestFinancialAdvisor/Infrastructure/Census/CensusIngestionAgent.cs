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

        // ACS 1-Year Estimates Variables
        // B19049: Median Household Income by Age of Householder
        // _002E: Under 25 years
        // _003E: 25 to 44 years
        // _004E: 45 to 64 years
        // _005E: 65 years and over
        private static readonly Dictionary<string, (int Min, int Max)> AgeVariables = new()
        {
            { "B19049_002E", (15, 24) }, // Census often starts "Under 25" effectively at 15 for householders
            { "B19049_003E", (25, 44) },
            { "B19049_004E", (45, 64) },
            { "B19049_005E", (65, 99) }
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
            _citationManager = new WorksCitedManager(rootPath);
        }

        public async Task RunIngestionAsync()
        {
            // 1. Discovery: Try to find latest data (simplification: assume 2022 is latest available stable for now)
            int year = 2022;
            string dataset = "acs1";

            Console.WriteLine($"[CensusAgent] Starting ingestion for {year} ACS 1-Year Estimates...");

            // 2. Extraction
            string variables = string.Join(",", AgeVariables.Keys);
            string jsonResponse = await _apiClient.FetchDataAsync(year, dataset, "B19049", variables);

            if (string.IsNullOrEmpty(jsonResponse))
            {
                Console.WriteLine("[CensusAgent] Failed to fetch data. Aborting.");
                return;
            }

            // 3. Normalization & Persistence
            var benchmarks = ParseCensusResponse(jsonResponse, year);

            Console.WriteLine($"[CensusAgent] Extracted {benchmarks.Count} benchmark records.");

            await _benchmarkProvider.BatchInsertBenchmarksAsync(benchmarks);

            // 4. Citation
            await _citationManager.AddCitationAsync(
                "American Community Survey: Median Household Income by Age of Householder (B19049)",
                "U.S. Census Bureau",
                year.ToString(),
                $"https://api.census.gov/data/{year}/acs/{dataset}",
                DateTime.Now.ToString("yyyy-MM-dd")
            );

            Console.WriteLine("[CensusAgent] Ingestion complete and cited.");
        }

        private List<IncomeBenchmark> ParseCensusResponse(string json, int year)
        {
            // Census API returns [[header1, header2...], [val1, val2...], ...]
            var benchmarks = new List<IncomeBenchmark>();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.GetArrayLength() < 2) return benchmarks; // No complex error handling vs simple empty check

            // Map header names to indices
            var headers = root[0];
            var headerMap = new Dictionary<string, int>();
            for (int i = 0; i < headers.GetArrayLength(); i++)
            {
                string? header = headers[i].GetString();
                if (!string.IsNullOrEmpty(header))
                {
                    headerMap[header] = i;
                }
            }

            // Iterate rows
            for (int i = 1; i < root.GetArrayLength(); i++)
            {
                var row = root[i];
                // Get State (last column usually, or look up "state")
                if (!headerMap.ContainsKey("state")) continue;

                var stateElement = row[headerMap["state"]];
                string? stateCode = stateElement.ValueKind == JsonValueKind.String ? stateElement.GetString() : stateElement.ToString();

                if (headerMap.ContainsKey("state") && FipsToState.TryGetValue(stateCode ?? "", out var stateAbbrev))
                {
                    stateCode = stateAbbrev;
                }
                else
                {
                    // If we can't map it, skip it, or strictly require mapping. 
                    // For now, let's skip unknown FIPS to keep DB clean with only valid abbreviations.
                    continue;
                }

                foreach (var kvp in AgeVariables)
                {
                    string varId = kvp.Key;
                    if (!headerMap.ContainsKey(varId)) continue;

                    int colIndex = headerMap[varId];
                    if (colIndex >= row.GetArrayLength()) continue; // Safety check

                    var valElement = row[colIndex];
                    string? valStr = valElement.ValueKind == JsonValueKind.String ? valElement.GetString() : valElement.ToString();

                    if (decimal.TryParse(valStr, out decimal income) && income > 0)
                    {
                        // Create Benchmark Record
                        benchmarks.Add(new IncomeBenchmark(
                            kvp.Value.Min,
                            kvp.Value.Max,
                            stateCode, // Confirmed non-null above
                            income,
                            income, // Using Median for Average too as generic fallback since table provides Median
                            null, // Gender null for this table
                            "Census",
                            year
                        ));
                    }
                }
            }

            return benchmarks;
        }
    }
}
