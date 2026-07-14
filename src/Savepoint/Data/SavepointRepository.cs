using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Savepoint.Data;

public sealed class SavepointRepository
{
    private readonly string _connectionString;

    public SavepointRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Savepoints (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Icon TEXT NOT NULL,
                ScheduleType INTEGER NOT NULL,
                InterruptionLevel INTEGER NOT NULL,
                IsEnabled INTEGER NOT NULL,
                SnoozeCount INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                TimeOfDay TEXT NULL,
                DayOfWeek INTEGER NULL,
                IntervalTicks INTEGER NULL,
                OneTimeAt TEXT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public int Add(SavepointEntry entry)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Savepoints
                (Name, Icon, ScheduleType, InterruptionLevel, IsEnabled, SnoozeCount, CreatedAt, TimeOfDay, DayOfWeek, IntervalTicks, OneTimeAt)
            VALUES
                ($name, $icon, $scheduleType, $level, $enabled, $snoozeCount, $createdAt, $timeOfDay, $dayOfWeek, $intervalTicks, $oneTimeAt);
            SELECT last_insert_rowid();
            """;
        BindParameters(command, entry);
        entry.Id = Convert.ToInt32(command.ExecuteScalar());
        return entry.Id;
    }

    public void Update(SavepointEntry entry)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Savepoints SET
                Name = $name, Icon = $icon, ScheduleType = $scheduleType, InterruptionLevel = $level,
                IsEnabled = $enabled, SnoozeCount = $snoozeCount, CreatedAt = $createdAt,
                TimeOfDay = $timeOfDay, DayOfWeek = $dayOfWeek, IntervalTicks = $intervalTicks, OneTimeAt = $oneTimeAt
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", entry.Id);
        BindParameters(command, entry);
        command.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Savepoints WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public SavepointEntry? GetById(int id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Savepoints WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadEntry(reader) : null;
    }

    public List<SavepointEntry> GetAll()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Savepoints ORDER BY Id;";
        using var reader = command.ExecuteReader();

        var results = new List<SavepointEntry>();
        while (reader.Read())
        {
            results.Add(ReadEntry(reader));
        }
        return results;
    }

    private static void BindParameters(SqliteCommand command, SavepointEntry entry)
    {
        command.Parameters.AddWithValue("$name", entry.Name);
        command.Parameters.AddWithValue("$icon", entry.Icon);
        command.Parameters.AddWithValue("$scheduleType", (int)entry.ScheduleType);
        command.Parameters.AddWithValue("$level", (int)entry.InterruptionLevel);
        command.Parameters.AddWithValue("$enabled", entry.IsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$snoozeCount", entry.SnoozeCount);
        command.Parameters.AddWithValue("$createdAt", entry.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$timeOfDay", (object?)entry.TimeOfDay?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$dayOfWeek", (object?)(entry.DayOfWeek.HasValue ? (int)entry.DayOfWeek.Value : null) ?? DBNull.Value);
        command.Parameters.AddWithValue("$intervalTicks", (object?)entry.Interval?.Ticks ?? DBNull.Value);
        command.Parameters.AddWithValue("$oneTimeAt", (object?)entry.OneTimeAt?.ToString("o", CultureInfo.InvariantCulture) ?? DBNull.Value);
    }

    private static SavepointEntry ReadEntry(SqliteDataReader reader)
    {
        return new SavepointEntry
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Icon = reader.GetString(reader.GetOrdinal("Icon")),
            ScheduleType = (ScheduleType)reader.GetInt32(reader.GetOrdinal("ScheduleType")),
            InterruptionLevel = (InterruptionLevel)reader.GetInt32(reader.GetOrdinal("InterruptionLevel")),
            IsEnabled = reader.GetInt32(reader.GetOrdinal("IsEnabled")) != 0,
            SnoozeCount = reader.GetInt32(reader.GetOrdinal("SnoozeCount")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            TimeOfDay = reader.IsDBNull(reader.GetOrdinal("TimeOfDay"))
                ? null
                : TimeOnly.Parse(reader.GetString(reader.GetOrdinal("TimeOfDay")), CultureInfo.InvariantCulture),
            DayOfWeek = reader.IsDBNull(reader.GetOrdinal("DayOfWeek"))
                ? null
                : (DayOfWeek)reader.GetInt32(reader.GetOrdinal("DayOfWeek")),
            Interval = reader.IsDBNull(reader.GetOrdinal("IntervalTicks"))
                ? null
                : TimeSpan.FromTicks(reader.GetInt64(reader.GetOrdinal("IntervalTicks"))),
            OneTimeAt = reader.IsDBNull(reader.GetOrdinal("OneTimeAt"))
                ? null
                : DateTime.Parse(reader.GetString(reader.GetOrdinal("OneTimeAt")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        };
    }
}
