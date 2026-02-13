using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SmartVestFinancialAdvisor.Infrastructure.Benchmarks;

namespace SmartVestFinancialAdvisor.Infrastructure.Census
{
    public class CensusBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);
        private readonly TimeSpan _updateFrequency = TimeSpan.FromDays(30); // Check for new Census data monthly

        public CensusBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var benchmarkProvider = scope.ServiceProvider.GetRequiredService<SqliteBenchmarkProvider>();

                    try
                    {
                        var lastUpdate = await benchmarkProvider.GetLastUpdateAsync();

                        if (DateTime.UtcNow - lastUpdate > _updateFrequency)
                        {
                            Console.WriteLine("[BackgroundService] Census data update required.");

                            // Initialize Agent with paths
                            string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "benchmarks.db");
                            string rootPath = AppDomain.CurrentDomain.BaseDirectory;
                            var agent = new CensusIngestionAgent(dbPath, rootPath);

                            await agent.RunIngestionAsync();
                            await benchmarkProvider.SetLastUpdateAsync(DateTime.UtcNow);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BackgroundService] Critical Error: {ex.Message}");
                    }
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
}