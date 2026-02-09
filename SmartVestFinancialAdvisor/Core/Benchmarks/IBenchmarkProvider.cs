using System.Threading.Tasks;

namespace SmartVestFinancialAdvisor.Core.Benchmarks
{
    /// <summary>
    /// Abstraction for retrieving financial benchmark data.
    /// </summary>
    public interface IBenchmarkProvider
    {
        /// <summary>
        /// Retrieves the most relevant income benchmark for the given demographic.
        /// </summary>
        /// <param name="age">Client's age.</param>
        /// <param name="state">Client's location (State code, e.g., "NY").</param>
        /// <param name="gender">Optional gender for more specific comparison.</param>
        /// <returns>Matching income benchmark or null if no data found.</returns>
        Task<IncomeBenchmark?> GetIncomeBenchmarkAsync(int age, string state, Gender? gender = null);
    }
}
