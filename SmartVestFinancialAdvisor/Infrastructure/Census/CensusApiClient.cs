using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmartVestFinancialAdvisor.Infrastructure.Census
{
    public class CensusApiClient
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.census.gov/data";

        public CensusApiClient()
        {
            _httpClient = new HttpClient();
        }

        public async Task<string> FetchDataAsync(int year, string dataset, string tableId, string variables)
        {
            // Example URL: https://api.census.gov/data/2021/acs/acs1?get=NAME,B19049_001E&for=state:*
            string url = $"{BaseUrl}/{year}/acs/{dataset}?get=NAME,{variables}&for=state:*";

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException e)
            {
                // Simple error handling for now - returns null to indicate failure/missing data
                Console.WriteLine($"Error fetching Census data: {e.Message}");
                return string.Empty;
            }
        }
    }
}
