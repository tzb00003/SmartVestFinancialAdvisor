using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartVestFinancialAdvisor.Core.Benchmarks
{
    public interface IBenchmarkProvider
    {
        /// <summary>
        /// Retrieves the definitive individual benchmark for a user's specific age and location.
        /// </summary>
        Task<IncomeBenchmark?> GetIncomeBenchmarkAsync(int userAge, string state, Gender? gender = null);

        /// <summary>
        /// Batch saves benchmarks (used by the CensusIngestionAgent).
        /// </summary>
        Task BatchInsertBenchmarksAsync(IEnumerable<IncomeBenchmark> benchmarks);

        /// <summary>
        /// Metadata management for background service sync.
        /// </summary>
        Task<DateTime> GetLastUpdateAsync();
        Task SetLastUpdateAsync(DateTime date);
    }
}
