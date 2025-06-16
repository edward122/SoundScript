using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SoundScript.Models;
using System.Diagnostics;

namespace SoundScript.Services
{
    public class HistoryService : IDisposable
    {
        private readonly string _connectionString;
        private readonly string _dbPath;

        public HistoryService()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoundScript");
            Directory.CreateDirectory(appDataPath);
            
            _dbPath = Path.Combine(appDataPath, "history.db");
            _connectionString = $"Data Source={_dbPath}";
            
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS DictationSessions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    RawText TEXT NOT NULL,
                    PolishedText TEXT NOT NULL,
                    Duration TEXT NOT NULL,
                    WordCount INTEGER NOT NULL,
                    WordsPerMinute REAL NOT NULL,
                    Confidence REAL NOT NULL,
                    Status TEXT NOT NULL,
                    ErrorMessage TEXT NOT NULL,
                    CharacterCount INTEGER NOT NULL,
                    SentenceCount INTEGER NOT NULL,
                    Language TEXT NOT NULL,
                    ModelUsed TEXT NOT NULL,
                    AudioData BLOB
                );";
            createTableCommand.ExecuteNonQuery();
            
            // Add AudioData column to existing tables (migration)
            try
            {
                var alterTableCommand = connection.CreateCommand();
                alterTableCommand.CommandText = "ALTER TABLE DictationSessions ADD COLUMN AudioData BLOB;";
                alterTableCommand.ExecuteNonQuery();
            }
            catch
            {
                // Column already exists, ignore error
            }
        }

        public async Task<int> SaveSessionAsync(DictationSession session)
        {
            session.CalculateStats();
            
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO DictationSessions 
                (Timestamp, RawText, PolishedText, Duration, WordCount, WordsPerMinute, 
                 Confidence, Status, ErrorMessage, CharacterCount, SentenceCount, Language, ModelUsed, AudioData)
                VALUES 
                (@timestamp, @rawText, @polishedText, @duration, @wordCount, @wordsPerMinute,
                 @confidence, @status, @errorMessage, @characterCount, @sentenceCount, @language, @modelUsed, @audioData);
                SELECT last_insert_rowid();";

            command.Parameters.AddWithValue("@timestamp", session.Timestamp.ToString("O"));
            command.Parameters.AddWithValue("@rawText", session.RawText);
            command.Parameters.AddWithValue("@polishedText", session.PolishedText);
            command.Parameters.AddWithValue("@duration", session.Duration.ToString());
            command.Parameters.AddWithValue("@wordCount", session.WordCount);
            command.Parameters.AddWithValue("@wordsPerMinute", session.WordsPerMinute);
            command.Parameters.AddWithValue("@confidence", session.Confidence);
            command.Parameters.AddWithValue("@status", session.Status);
            command.Parameters.AddWithValue("@errorMessage", session.ErrorMessage);
            command.Parameters.AddWithValue("@characterCount", session.CharacterCount);
            command.Parameters.AddWithValue("@sentenceCount", session.SentenceCount);
            command.Parameters.AddWithValue("@language", session.Language);
            command.Parameters.AddWithValue("@modelUsed", session.ModelUsed);
            command.Parameters.AddWithValue("@audioData", session.AudioData ?? (object)DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<List<DictationSession>> GetRecentSessionsAsync(int count = 50)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM DictationSessions 
                ORDER BY Timestamp DESC 
                LIMIT @count";
            command.Parameters.AddWithValue("@count", count);

            var sessions = new List<DictationSession>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                sessions.Add(MapReaderToSession(reader));
            }

            return sessions;
        }

        public async Task<List<DictationSession>> GetSessionsByDateAsync(DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM DictationSessions 
                WHERE Timestamp >= @start AND Timestamp < @end
                ORDER BY Timestamp DESC";
            command.Parameters.AddWithValue("@start", startOfDay.ToString("O"));
            command.Parameters.AddWithValue("@end", endOfDay.ToString("O"));

            var sessions = new List<DictationSession>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                sessions.Add(MapReaderToSession(reader));
            }

            return sessions;
        }

        public async Task<DictationSession?> GetSessionByIdAsync(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM DictationSessions WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapReaderToSession(reader);
            }

            return null;
        }

        public async Task<HistoryStats> GetStatsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    COUNT(*) as TotalSessions,
                    COALESCE(SUM(WordCount), 0) as TotalWords,
                    COALESCE(AVG(WordsPerMinute), 0) as AvgWPM,
                    COALESCE(AVG(Confidence), 0) as AvgConfidence
                FROM DictationSessions 
                WHERE Status = 'Completed'";

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var stats = new HistoryStats
                {
                    TotalSessions = Convert.ToInt32(reader["TotalSessions"]),
                    TotalWords = Convert.ToInt32(reader["TotalWords"]),
                    AverageWPM = Convert.ToDouble(reader["AvgWPM"]),
                    AverageConfidence = Convert.ToDouble(reader["AvgConfidence"])
                };

                // Close the first reader before opening a new command
                reader.Close();

                // Calculate total time separately
                var totalTimeCommand = connection.CreateCommand();
                totalTimeCommand.CommandText = "SELECT Duration FROM DictationSessions WHERE Status = 'Completed'";
                var totalTime = TimeSpan.Zero;
                
                using var timeReader = await totalTimeCommand.ExecuteReaderAsync();
                while (await timeReader.ReadAsync())
                {
                    if (TimeSpan.TryParse(timeReader["Duration"].ToString(), out var duration))
                    {
                        totalTime = totalTime.Add(duration);
                    }
                }

                stats.TotalRecordingTime = totalTime;
                return stats;
            }

            return new HistoryStats();
        }

        public async Task<List<DictationSession>> SearchSessionsAsync(string searchTerm)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM DictationSessions 
                WHERE RawText LIKE @search OR PolishedText LIKE @search
                ORDER BY Timestamp DESC 
                LIMIT 100";
            command.Parameters.AddWithValue("@search", $"%{searchTerm}%");

            var sessions = new List<DictationSession>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                sessions.Add(MapReaderToSession(reader));
            }

            return sessions;
        }

        public async Task DeleteSessionAsync(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM DictationSessions WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<DictationSession>> GetMoreSessionsAsync(int skip = 0, int take = 20)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM DictationSessions 
                ORDER BY Timestamp DESC 
                LIMIT @take OFFSET @skip";
            command.Parameters.AddWithValue("@take", take);
            command.Parameters.AddWithValue("@skip", skip);

            var sessions = new List<DictationSession>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                sessions.Add(MapReaderToSession(reader));
            }

            return sessions;
        }

        public async Task<List<DictationSession>> GetAllSessionsAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT * FROM DictationSessions 
                    ORDER BY Timestamp DESC";
                
                var sessions = new List<DictationSession>();
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    sessions.Add(MapReaderToSession(reader));
                }
                
                return sessions;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting all sessions: {ex.Message}");
                return new List<DictationSession>();
            }
        }

        private DictationSession MapReaderToSession(SqliteDataReader reader)
        {
            // Handle AudioData safely (might not exist in older database versions)
            byte[]? audioData = null;
            try
            {
                if (reader["AudioData"] != DBNull.Value)
                {
                    audioData = (byte[])reader["AudioData"];
                }
            }
            catch
            {
                // AudioData column doesn't exist in older database versions
                audioData = null;
            }

            return new DictationSession
            {
                Id = Convert.ToInt32(reader["Id"]),
                Timestamp = DateTime.Parse(reader["Timestamp"].ToString() ?? ""),
                RawText = reader["RawText"].ToString() ?? "",
                PolishedText = reader["PolishedText"].ToString() ?? "",
                Duration = TimeSpan.Parse(reader["Duration"].ToString() ?? "00:00:00"),
                WordCount = Convert.ToInt32(reader["WordCount"]),
                WordsPerMinute = Convert.ToDouble(reader["WordsPerMinute"]),
                Confidence = Convert.ToDouble(reader["Confidence"]),
                Status = reader["Status"].ToString() ?? "",
                ErrorMessage = reader["ErrorMessage"].ToString() ?? "",
                CharacterCount = Convert.ToInt32(reader["CharacterCount"]),
                SentenceCount = Convert.ToInt32(reader["SentenceCount"]),
                Language = reader["Language"].ToString() ?? "",
                ModelUsed = reader["ModelUsed"].ToString() ?? "",
                AudioData = audioData
            };
        }

        public async Task ClearAllDataAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM DictationSessions";
            await command.ExecuteNonQueryAsync();
        }

        public void Dispose()
        {
            // SQLite connections are disposed automatically
        }
    }

    public class HistoryStats
    {
        public int TotalSessions { get; set; }
        public int TotalWords { get; set; }
        public double AverageWPM { get; set; }
        public TimeSpan TotalRecordingTime { get; set; }
        public double AverageConfidence { get; set; }
        
        public string FormattedTotalTime => TotalRecordingTime.TotalHours >= 1 
            ? $"{(int)TotalRecordingTime.TotalHours}h {TotalRecordingTime.Minutes}m"
            : $"{TotalRecordingTime.Minutes}m {TotalRecordingTime.Seconds}s";
    }
} 