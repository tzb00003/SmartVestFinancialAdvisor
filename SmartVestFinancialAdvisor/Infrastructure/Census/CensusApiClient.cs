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
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SmartVestAdvisor/1.0");
        }

        public async Task<string> FetchDataAsync(int year, string dataset, string variables)
        {
            // Direct path to Subject Tables: /data/2022/acs/acs1/subject
            // We use acs1 because it is the most current for S2001.
            string url = $"{BaseUrl}/{year}/acs/{dataset}/subject?get={variables}&for=state:*";

            try
            {
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Census API Error] Status: {response.StatusCode} for URL: {url}");
                    return string.Empty;
                }

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Census API Exception]: {e.Message}");
                return string.Empty;
            }
        }
    }
}