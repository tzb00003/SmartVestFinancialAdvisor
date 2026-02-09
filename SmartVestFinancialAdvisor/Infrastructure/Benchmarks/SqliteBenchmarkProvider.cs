using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SmartVestFinancialAdvisor.Core.Benchmarks;

namespace SmartVestFinancialAdvisor.Infrastructure.Benchmarks
{
    using System.Collections.Generic;

    public class SqliteBenchmarkProvider : IBenchmarkProvider
    {
        private readonly string _connectionString;

        public SqliteBenchmarkProvider(string dbPath)
        {
            _connectionString = $"Data Source={dbPath}";
            InitializeDatabase(dbPath);
        }

        private void InitializeDatabase(string dbPath)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            if (File.Exists(dbPath))
            {
                EnsureSchema(connection);
                return;
            }

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
                    Year INTEGER NOT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS IX_IncomeBenchmarks_Unique
                    ON IncomeBenchmarks (State, AgeMin, AgeMax, Gender, Source, Year);

                -- Seed Data
                INSERT INTO IncomeBenchmarks (State, AgeMin, AgeMax, Gender, MedianIncome, AverageIncome, Source, Year) VALUES 
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
            using var pragmaCmd = connection.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA table_info(IncomeBenchmarks);";

            var hasSource = false;
            var hasYear = false;

            using (var reader = pragmaCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "Source", StringComparison.OrdinalIgnoreCase)) hasSource = true;
                    if (string.Equals(name, "Year", StringComparison.OrdinalIgnoreCase)) hasYear = true;
                }
            }

            if (!hasSource)
            {
                using var addSource = connection.CreateCommand();
                addSource.CommandText = "ALTER TABLE IncomeBenchmarks ADD COLUMN Source TEXT NOT NULL DEFAULT 'Seed';";
                addSource.ExecuteNonQuery();
            }

            if (!hasYear)
            {
                using var addYear = connection.CreateCommand();
                addYear.CommandText = "ALTER TABLE IncomeBenchmarks ADD COLUMN Year INTEGER NOT NULL DEFAULT 0;";
                addYear.ExecuteNonQuery();
            }

            using var addIndex = connection.CreateCommand();
            addIndex.CommandText = @"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_IncomeBenchmarks_Unique
                    ON IncomeBenchmarks (State, AgeMin, AgeMax, Gender, Source, Year);
            ";
            addIndex.ExecuteNonQuery();
        }

        public async Task<IncomeBenchmark?> GetIncomeBenchmarkAsync(int age, string state, Gender? gender = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var baseQuery = @"
                SELECT AgeMin, AgeMax, State, Gender, MedianIncome, AverageIncome, Source, Year 
                FROM IncomeBenchmarks 
                WHERE State = @State 
                  AND @Age >= AgeMin 
                  AND @Age <= AgeMax
            ";
            var orderClause = " ORDER BY COALESCE(Year, 0) DESC, CASE WHEN Source = 'Census' THEN 1 ELSE 0 END DESC";

            // If gender is provided, try to find a specific match first
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

            // Fallback: Try to find a gender-neutral record (Gender IS NULL)
            // Or if we didn't search for gender, just find the general record.
            // Note: If the specific gender query failed, we come here. 
            // We search for Gender IS NULL specifically to avoid getting a random gendered record if gender was not requested or not found.
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
                reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
            );
        }


        public async Task BatchInsertBenchmarksAsync(IEnumerable<IncomeBenchmark> benchmarks)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            var command = connection.CreateCommand();
            command.Transaction = transaction;

            // Use INSERT OR REPLACE to leverage your Unique Index
            command.CommandText = @"
                INSERT OR REPLACE INTO IncomeBenchmarks 
                (State, AgeMin, AgeMax, Gender, MedianIncome, AverageIncome, Source, Year) 
                VALUES (@State, @AgeMin, @AgeMax, @Gender, @MedianIncome, @AverageIncome, @Source, @Year)
            ";

            var pState = command.Parameters.Add("@State", SqliteType.Text);
            var pAgeMin = command.Parameters.Add("@AgeMin", SqliteType.Integer);
            var pAgeMax = command.Parameters.Add("@AgeMax", SqliteType.Integer);
            var pGender = command.Parameters.Add("@Gender", SqliteType.Text);
            var pMedian = command.Parameters.Add("@MedianIncome", SqliteType.Real);
            var pAverage = command.Parameters.Add("@AverageIncome", SqliteType.Real);
            var pSource = command.Parameters.Add("@Source", SqliteType.Text);
            var pYear = command.Parameters.Add("@Year", SqliteType.Integer);

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

            if (result != null && DateTime.TryParse(result.ToString(), out var date))
            {
                return date;
            }

            return DateTime.MinValue;
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
