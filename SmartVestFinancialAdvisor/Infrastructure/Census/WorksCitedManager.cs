using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SmartVestFinancialAdvisor.Infrastructure.Census
{
    /// <summary>
    /// Manages the generation and maintenance of the WorksCited.md document 
    /// to ensure all data sources are properly attributed in MLA format.
    /// </summary>
    public class WorksCitedManager
    {
        private readonly string _filePath;

        /// <summary>
        /// Initializes a new instance of the manager.
        /// </summary>
        /// <param name="rootPath">The base directory where WorksCited.md should be stored.</param>
        public WorksCitedManager(string rootPath)
        {
            _filePath = Path.Combine(rootPath, "WorksCited.md");
        }

        /// <summary>
        /// Appends a new MLA-style citation to the Works Cited document.
        /// </summary>
        /// <param name="title">Title of the data table or dataset.</param>
        /// <param name="publisher">The publishing organization (e.g., "U.S. Census Bureau").</param>
        /// <param name="year">The data year.</param>
        /// <param name="url">The permanent URL to the data source.</param>
        /// <param name="accessDate">The date the data was retrieved.</param>
        public async Task AddCitationAsync(string title, string publisher, string year, string url, string accessDate)
        {
            string citation = $"{publisher}. \"{title}.\" *{publisher}*, {year}, {url}. Accessed {accessDate}.";

            string existingContent = string.Empty;
            if (File.Exists(_filePath))
            {
                existingContent = await File.ReadAllTextAsync(_filePath);
                if (existingContent.IndexOf(url, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }
            }

            using (var writer = new StreamWriter(_filePath, append: true))
            {
                if (string.IsNullOrWhiteSpace(existingContent))
                {
                    await writer.WriteLineAsync("# Works Cited");
                    await writer.WriteLineAsync("");
                }

                // Append the citation as a list item
                await writer.WriteLineAsync($"- {citation}");
            }
        }
    }
}
