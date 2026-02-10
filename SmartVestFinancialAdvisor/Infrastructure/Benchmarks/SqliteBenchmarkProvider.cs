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
            bool dbExists = File.Exists(dbPath);

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                if (dbExists)
                {
                    EnsureSchema(connection);
                }
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
                    Gender TEXT,
                    MedianIncome DECIMAL NOT NULL,
                    AverageIncome DECIMAL NOT NULL,
                    Source TEXT NOT NULL,
                    Year INTEGER NOT NULL,
                    P10 DECIMAL,
                    P25 DECIMAL,
                    P75 DECIMAL,
                    P90 DECIMAL,
                    P95 DECIMAL,
                    P99 DECIMAL,
                    P99_9 DECIMAL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS IX_IncomeBenchmarks_Unique
                    ON IncomeBenchmarks (State, AgeMin, AgeMax, Gender, Source, Year);

                -- Seed Data
                INSERT OR IGNORE INTO IncomeBenchmarks (State, AgeMin, AgeMax, Gender, MedianIncome, AverageIncome, Source, Year) VALUES 
                ('NY', 25, 34, NULL, 65000, 78000, 'Seed', 0),
                ('NY', 35, 44, NULL, 85000, 95000, 'Seed', 0),
                ('NY', 25, 34, 'Male', 70000, 82000, 'Seed', 0),
                ('NY', 25, 34, 'Female', 68000, 80000, 'Seed', 0),
                ('CA', 25, 34, NULL, 70000, 85000, 'Seed', 0),
                ('TX', 25, 34, NULL, 55000, 65000, 'Seed', 0);
            ";
            command.ExecuteNonQuery();
        }

        private void EnsureSchema(SqliteConnection connection)
        {
            // Check if table exists before running pragma
            var checkTab = connection.CreateCommand();
            checkTab.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='IncomeBenchmarks';";
            if (checkTab.ExecuteScalar() == null) return;

            using var pragmaCmd = connection.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA table_info(IncomeBenchmarks);";

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = pragmaCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    columns.Add(reader.GetString(1));
                }
            }

            string[] expectedColumns = { "Source", "Year", "P10", "P25", "P75", "P90", "P95", "P99", "P99_9" };
            foreach (var col in expectedColumns)
            {
                if (!columns.Contains(col))
                {
                    using var addCol = connection.CreateCommand();
                    string type = (col == "Source") ? "TEXT NOT NULL DEFAULT 'Seed'" :
                                 (col == "Year") ? "INTEGER NOT NULL DEFAULT 0" : "DECIMAL";
                    addCol.CommandText = $"ALTER TABLE IncomeBenchmarks ADD COLUMN {col} {type};";
                    addCol.ExecuteNonQuery();
                }
            }
        }

        public async Task<IncomeBenchmark?> GetIncomeBenchmarkAsync(int age, string state, Gender? gender = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var baseQuery = @"
                SELECT AgeMin, AgeMax, State, Gender, MedianIncome, AverageIncome, Source, Year,
                       P10, P25, P75, P90, P95, P99, P99_9
                FROM IncomeBenchmarks 
                WHERE State = @State 
                  AND @Age >= AgeMin 
                  AND @Age <= AgeMax
            ";
            var orderClause = " ORDER BY Year DESC, CASE WHEN Source = 'Census' THEN 1 ELSE 0 END DESC";

            if (gender.HasValue)
            {
                var genderQuery = baseQuery + " AND Gender = @Gender" + orderClause;
                using var genderCmd = connection.CreateCommand();
                genderCmd.CommandText = genderQuery;
                genderCmd.Parameters.AddWithValue("@State", state);
                genderCmd.Parameters.AddWithValue("@Age", age);
                genderCmd.Parameters.AddWithValue("@Gender", gender.Value.ToString());

                using var reader = await genderCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapReaderToBenchmark(reader);
                }
            }

            var fallbackQuery = baseQuery + " AND Gender IS NULL" + orderClause;
            using var fallbackCmd = connection.CreateCommand();
            fallbackCmd.CommandText = fallbackQuery;
            fallbackCmd.Parameters.AddWithValue("@State", state);
            fallbackCmd.Parameters.AddWithValue("@Age", age);

            using var fallbackReader = await fallbackCmd.ExecuteReaderAsync();
            if (await fallbackReader.ReadAsync())
            {
                return MapReaderToBenchmark(fallbackReader);
            }

            return null;
        }

        public async Task<decimal?> GetTopTierIncomeCeilingAsync(int age, string state, Gender? gender = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var latestYearCmd = connection.CreateCommand();
            latestYearCmd.CommandText = @"
                SELECT MAX(Year)
                FROM IncomeBenchmarks
                WHERE State = @State
                  AND @Age >= AgeMin
                  AND @Age <= AgeMax
            ";
            latestYearCmd.Parameters.AddWithValue("@State", state);
            latestYearCmd.Parameters.AddWithValue("@Age", age);

            var latestYearObj = await latestYearCmd.ExecuteScalarAsync();
            if (latestYearObj == null || latestYearObj == DBNull.Value) return null;
            var latestYear = Convert.ToInt32(latestYearObj);

            var censusExistsCmd = connection.CreateCommand();
            censusExistsCmd.CommandText = @"
                SELECT 1 FROM IncomeBenchmarks
                WHERE State = @State AND Year = @Year AND @Age >= AgeMin AND @Age <= AgeMax AND Source = 'Census'
                AND ((@Gender IS NULL AND Gender IS NULL) OR Gender = @Gender)
                LIMIT 1
            ";
            censusExistsCmd.Parameters.AddWithValue("@State", state);
            censusExistsCmd.Parameters.AddWithValue("@Year", latestYear);
            censusExistsCmd.Parameters.AddWithValue("@Age", age);
            censusExistsCmd.Parameters.AddWithValue("@Gender", gender.HasValue ? gender.Value.ToString() : DBNull.Value);

            var hasCensus = await censusExistsCmd.ExecuteScalarAsync() != null;

            var ceilingCmd = connection.CreateCommand();
            ceilingCmd.CommandText = @"
                SELECT COALESCE(MAX(P95), MAX(MedianIncome))
                FROM IncomeBenchmarks
                WHERE State = @State AND Year = @Year AND @Age >= AgeMin AND @Age <= AgeMax
                  AND ((@Gender IS NULL AND Gender IS NULL) OR Gender = @Gender)
                  AND (@UseCensus = 0 OR Source = 'Census')
            ";
            ceilingCmd.Parameters.AddWithValue("@State", state);
            ceilingCmd.Parameters.AddWithValue("@Year", latestYear);
            ceilingCmd.Parameters.AddWithValue("@Age", age);
            ceilingCmd.Parameters.AddWithValue("@Gender", gender.HasValue ? gender.Value.ToString() : DBNull.Value);
            ceilingCmd.Parameters.AddWithValue("@UseCensus", hasCensus ? 1 : 0);

            var ceilingObj = await ceilingCmd.ExecuteScalarAsync();
            return (ceilingObj == null || ceilingObj == DBNull.Value) ? null : Convert.ToDecimal(ceilingObj);
        }

        private IncomeBenchmark MapReaderToBenchmark(SqliteDataReader reader)
        {
            var genderStr = reader.IsDBNull(3) ? null : reader.GetString(3);
            Gender? gender = genderStr != null && Enum.TryParse<Gender>(genderStr, out var g) ? g : null;

            return new IncomeBenchmark(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetDecimal(4),
                reader.GetDecimal(5),
                gender,
                reader.IsDBNull(6) ? "Seed" : reader.GetString(6),
                reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                reader.IsDBNull(9) ? null : reader.GetDecimal(9),
                reader.IsDBNull(10) ? null : reader.GetDecimal(10),
                reader.IsDBNull(11) ? null : reader.GetDecimal(11),
                reader.IsDBNull(12) ? null : reader.GetDecimal(12),
                reader.IsDBNull(13) ? null : reader.GetDecimal(13),
                reader.IsDBNull(14) ? null : reader.GetDecimal(14)
            );
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
                (State, AgeMin, AgeMax, Gender, MedianIncome, AverageIncome, Source, Year, 
                 P10, P25, P75, P90, P95, P99, P99_9) 
                VALUES (@State, @AgeMin, @AgeMax, @Gender, @MedianIncome, @AverageIncome, @Source, @Year,
                        @P10, @P25, @P75, @P90, @P95, @P99, @P99_9)
            ";

            var pState = command.Parameters.Add("@State", SqliteType.Text);
            var pAgeMin = command.Parameters.Add("@AgeMin", SqliteType.Integer);
            var pAgeMax = command.Parameters.Add("@AgeMax", SqliteType.Integer);
            var pGender = command.Parameters.Add("@Gender", SqliteType.Text);
            var pMedian = command.Parameters.Add("@MedianIncome", SqliteType.Real);
            var pAverage = command.Parameters.Add("@AverageIncome", SqliteType.Real);
            var pSource = command.Parameters.Add("@Source", SqliteType.Text);
            var pYear = command.Parameters.Add("@Year", SqliteType.Integer);
            var pP10 = command.Parameters.Add("@P10", SqliteType.Real);
            var pP25 = command.Parameters.Add("@P25", SqliteType.Real);
            var pP75 = command.Parameters.Add("@P75", SqliteType.Real);
            var pP90 = command.Parameters.Add("@P90", SqliteType.Real);
            var pP95 = command.Parameters.Add("@P95", SqliteType.Real);
            var pP99 = command.Parameters.Add("@P99", SqliteType.Real);
            var pP99_9 = command.Parameters.Add("@P99_9", SqliteType.Real);

            foreach (var b in benchmarks)
            {
                pState.Value = b.State;
                pAgeMin.Value = b.AgeRangeMin;
                pAgeMax.Value = b.AgeRangeMax;
                pGender.Value = b.Gender.HasValue ? b.Gender.ToString() : DBNull.Value;
                pMedian.Value = b.MedianIncome;
                pAverage.Value = b.AverageIncome;
                pSource.Value = b.Source ?? "Seed";
                pYear.Value = b.Year;
                pP10.Value = (object?)b.P10 ?? DBNull.Value;
                pP25.Value = (object?)b.P25 ?? DBNull.Value;
                pP75.Value = (object?)b.P75 ?? DBNull.Value;
                pP90.Value = (object?)b.P90 ?? DBNull.Value;
                pP95.Value = (object?)b.P95 ?? DBNull.Value;
                pP99.Value = (object?)b.P99 ?? DBNull.Value;
                pP99_9.Value = (object?)b.P99_9 ?? DBNull.Value;

                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        public async Task<DateTime> GetLastUpdateAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM SystemMetadata WHERE Key = 'LastCensusUpdate'";
            var result = await command.ExecuteScalarAsync();
            return (result != null && DateTime.TryParse(result.ToString(), out var date)) ? date : DateTime.MinValue;
        }

        public async Task SetLastUpdateAsync(DateTime date)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE SystemMetadata SET Value = @Value WHERE Key = 'LastCensusUpdate'";
            command.Parameters.AddWithValue("@Value", date.ToString("o"));
            await command.ExecuteNonQueryAsync();
        }
    }
}
