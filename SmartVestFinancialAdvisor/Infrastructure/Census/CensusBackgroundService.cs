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
        // private readonly TimeSpan _updateFrequency = TimeSpan.FromDays(365);
        private readonly TimeSpan _updateFrequency = TimeSpan.FromMinutes(1); // DEBUG: Reduced for testing (1 min), will set to 365 days in production

        public CensusBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial delay to let app start
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var benchmarkProvider = scope.ServiceProvider.GetRequiredService<SqliteBenchmarkProvider>();

                    // We need a way to run the ingestion. The agent itself requires params or we can wrap it.
                    // For now, let's instantiate it manually here as it's not registered in DI yet as a service.
                    // In a real app, we'd register ICensusIngestionAgent.
                    string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "benchmarks.db");
                    string rootPath = AppDomain.CurrentDomain.BaseDirectory;
                    var agent = new CensusIngestionAgent(dbPath, rootPath);

                    try
                    {
                        var lastUpdate = await benchmarkProvider.GetLastUpdateAsync();

                        // Check if we need to update
                        if (DateTime.UtcNow - lastUpdate > _updateFrequency)
                        {
                            Console.WriteLine("[BackgroundService] Starting scheduled Census update...");
                            await agent.RunIngestionAsync();
                            await benchmarkProvider.SetLastUpdateAsync(DateTime.UtcNow);
                            Console.WriteLine("[BackgroundService] Update complete. Next check in 24 hours.");
                        }
                        else
                        {
                            Console.WriteLine($"[BackgroundService] Data is up to date (Last: {lastUpdate}). Skipping.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BackgroundService] Error during update: {ex.Message}");
                    }
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
}
