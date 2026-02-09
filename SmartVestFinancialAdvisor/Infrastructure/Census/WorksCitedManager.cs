using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SmartVestFinancialAdvisor.Infrastructure.Census
{
    public class WorksCitedManager
    {
        private readonly string _filePath;

        public WorksCitedManager(string rootPath)
        {
            _filePath = Path.Combine(rootPath, "WorksCited.md");
        }

        public async Task AddCitationAsync(string title, string publisher, string year, string url, string accessDate)
        {
            string citation = $"{publisher}. \"{title}.\" *{publisher}*, {year}, {url}. Accessed {accessDate}.";

            // Check if file exists, if not create header
            bool fileExists = File.Exists(_filePath);

            using (var writer = new StreamWriter(_filePath, append: true))
            {
                if (!fileExists)
                {
                    await writer.WriteLineAsync("# Works Cited");
                    await writer.WriteLineAsync("");
                }

                // Simple duplicate check could be added here, but for now we append.
                // ideally we'd read the file and check if citation exists.
                await writer.WriteLineAsync($"- {citation}");
            }
        }
    }
}
