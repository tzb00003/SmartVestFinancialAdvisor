using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SmartVestFinancialAdvisor.Core.Benchmarks;
using System.Collections.Generic;

namespace SmartVestFinancialAdvisor.Infrastructure.Benchmarks
{
    public class SqliteBenchmarkProvider : IBenchmarkProvider
    {
        private readonly string _connectionString;

        public SqliteBenchmarkProvider(string dbPath)
        {
            _connectionString = $"Data Source={dbPath}";
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                InitializeTables(connection);
            }
        }

        private void InitializeTables(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS SystemMetadata (
                    Key TEXT PRIMARY KEY,
                    Value TEXT
                );

                INSERT OR IGNORE INTO SystemMetadata (Key, Value) VALUES ('LastCensusUpdate', '');

                CREATE TABLE IF NOT EXISTS IncomeBenchmarks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    State TEXT NOT NULL,
                    AgeMin INTEGER NOT NULL,
                    AgeMax INTEGER NOT NULL,
                    Gender TEXT, -- 'Male', 'Female', or NULL for Total
                    MedianIncome DECIMAL NOT NULL,
                    P25 DECIMAL NOT NULL,
                    P75 DECIMAL NOT NULL,
                    P95 DECIMAL NOT NULL,
                    Source TEXT NOT NULL, -- e.g., 'B20018'
                    Year INTEGER NOT NULL
                );

                -- Unique constraint ensures only one source of truth per demographic slice
                CREATE UNIQUE INDEX IF NOT EXISTS IX_IncomeBenchmarks_Sync 
                    ON IncomeBenchmarks (State, AgeMin, AgeMax, Gender, Year);
            ";
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Retrieves the precise individual benchmark. 
        /// Uses the 'Gender' preference to find the exact B20018 row.
        /// </summary>
        public async Task<IncomeBenchmark?> GetIncomeBenchmarkAsync(int userAge, string state, Gender? gender = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // We look for the most recent year that contains the user's age.
            // Priority: 1. Gender Match, 2. Highest Year
            var query = @"
                SELECT AgeMin, AgeMax, State, Gender, MedianIncome, P25, P75, P95, Year, Source
                FROM IncomeBenchmarks 
                WHERE State = @State 
                  AND @UserAge BETWEEN AgeMin AND AgeMax
                  AND (Gender = @Gender OR Gender IS NULL)
                ORDER BY (Gender = @Gender) DESC, Year DESC
                LIMIT 1";

            using var cmd = connection.CreateCommand();
            cmd.CommandText = query;
            cmd.Parameters.AddWithValue("@State", state);
            cmd.Parameters.AddWithValue("@UserAge", userAge);
            cmd.Parameters.AddWithValue("@Gender", gender.HasValue ? gender.Value.ToString() : DBNull.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var genderStr = reader.IsDBNull(3) ? null : reader.GetString(3);
                return new IncomeBenchmark
                {
                    AgeRangeMin = reader.GetInt32(0),
                    AgeRangeMax = reader.GetInt32(1),
                    State = reader.GetString(2),
                    Gender = genderStr != null ? Enum.Parse<Gender>(genderStr) : null,
                    MedianIncome = reader.GetDecimal(4),
                    P25 = reader.GetDecimal(5),
                    P75 = reader.GetDecimal(6),
                    P95 = reader.GetDecimal(7),
                    Year = reader.GetInt32(8),
                    Source = reader.GetString(9)
                };
            }
            return null;
        }

        public async Task BatchInsertBenchmarksAsync(IEnumerable<IncomeBenchmark> benchmarks)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                INSERT OR REPLACE INTO IncomeBenchmarks 
                (State, AgeMin, AgeMax, Gender, MedianIncome, P25, P75, P95, Source, Year) 
                VALUES (@State, @AgeMin, @AgeMax, @Gender, @Median, @P25, @P75, @P95, @Source, @Year)";

            // Reusable parameters for performance
            var pState = command.Parameters.Add("@State", SqliteType.Text);
            var pAgeMin = command.Parameters.Add("@AgeMin", SqliteType.Integer);
            var pAgeMax = command.Parameters.Add("@AgeMax", SqliteType.Integer);
            var pGender = command.Parameters.Add("@Gender", SqliteType.Text);
            var pMedian = command.Parameters.Add("@Median", SqliteType.Real);
            var pP25 = command.Parameters.Add("@P25", SqliteType.Real);
            var pP75 = command.Parameters.Add("@P75", SqliteType.Real);
            var pP95 = command.Parameters.Add("@P95", SqliteType.Real);
            var pSource = command.Parameters.Add("@Source", SqliteType.Text);
            var pYear = command.Parameters.Add("@Year", SqliteType.Integer);

            foreach (var b in benchmarks)
            {
                pState.Value = b.State;
                pAgeMin.Value = b.AgeRangeMin;
                pAgeMax.Value = b.AgeRangeMax;
                pGender.Value = b.Gender?.ToString() ?? (object)DBNull.Value;
                pMedian.Value = b.MedianIncome;
                pP25.Value = b.P25;
                pP75.Value = b.P75;
                pP95.Value = b.P95;
                pSource.Value = b.Source;
                pYear.Value = b.Year;

                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }

        public async Task<DateTime> GetLastUpdateAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var cmd = new SqliteCommand("SELECT Value FROM SystemMetadata WHERE Key = 'LastCensusUpdate'", connection);
            var val = await cmd.ExecuteScalarAsync();
            return DateTime.TryParse(val?.ToString(), out var dt) ? dt : DateTime.MinValue;
        }

        public async Task SetLastUpdateAsync(DateTime date)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var cmd = new SqliteCommand("UPDATE SystemMetadata SET Value = @v WHERE Key = 'LastCensusUpdate'", connection);
            cmd.Parameters.AddWithValue("@v", date.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
