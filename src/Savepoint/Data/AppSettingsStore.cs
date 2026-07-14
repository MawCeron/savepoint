using Microsoft.Data.Sqlite;

namespace Savepoint.Data;

public sealed class AppSettingsStore
{
    private const string AutostartOptOutKey = "AutostartOptOut";
    private const string FirstRunNoticeShownKey = "FirstRunNoticeShown";

    private readonly string _connectionString;

    public AppSettingsStore(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public bool AutostartOptOut
    {
        get => GetBool(AutostartOptOutKey);
        set => SetBool(AutostartOptOutKey, value);
    }

    public bool FirstRunNoticeShown
    {
        get => GetBool(FirstRunNoticeShownKey);
        set => SetBool(FirstRunNoticeShownKey, value);
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private bool GetBool(string key)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", key);
        var value = command.ExecuteScalar() as string;
        return value == "1";
    }

    private void SetBool(string key, bool value)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Settings (Key, Value) VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = $value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value ? "1" : "0");
        command.ExecuteNonQuery();
    }
}
