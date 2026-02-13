using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmartVestFinancialAdvisor.Infrastructure.Census
{
    /// <summary>
    /// Specialized client for fetching individual professional earnings data 
    /// from the U.S. Census Bureau ACS datasets.
    /// </summary>
    public class CensusApiClient
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.census.gov/data";

        public CensusApiClient()
        {
            // Best Practice: In production, consider using IHttpClientFactory
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Specifically fetches Table B20018 variables for working-age brackets.
        /// Covers: 18-24, 25-44, 45-64 for both Male and Female.
        /// </summary>
        public async Task<string> FetchWorkingAgeEarningsAsync(int year)
        {
            // Variable Map for B20018 (Full-Time workers):
            // 002E: Male 15-24 | 003E: Male 25-44 | 004E: Male 45-64
            // 007E: Female 15-24 | 008E: Female 25-44 | 009E: Female 45-64
            string variables = "B20018_002E,B20018_003E,B20018_004E,B20018_007E,B20018_008E,B20018_009E";

            return await FetchDataAsync(year, "acs1", variables);
        }

        /// <summary>
        /// Generic fetcher for ACS data.
        /// </summary>
        /// <param name="year">The year (e.g., 2022).</param>
        /// <param name="dataset">The ACS type ("acs1" for 1-yr, "acs5" for 5-yr fallback).</param>
        /// <param name="variables">Comma-separated variables.</param>
        public async Task<string> FetchDataAsync(int year, string dataset, string variables)
        {
            string url = $"{BaseUrl}/{year}/acs/{dataset}?get=NAME,{variables}&for=state:*";

            try
            {
                var response = await _httpClient.GetAsync(url);

                // If acs1 is missing (404), and we haven't tried acs5 yet
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound && dataset == "acs1")
                {
                    Console.WriteLine($"[Census API] {year} acs1 not found, falling back to acs5...");
                    return await FetchDataAsync(year, "acs5", variables);
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Census API Error]: {e.Message}");
                return string.Empty;
            }
        }
    }
}