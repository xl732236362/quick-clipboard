using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;
using QuickClipboard.Core.Models;
using QuickClipboard.Core.Services;

namespace QuickClipboard.Infrastructure.Persistence;

public sealed class SqliteSettingsRepository(Func<SqliteConnection> createConnection) : ISettingsRepository
{
    private const string PanelHotkeyKey = "panel_hotkey";
    private const string HistoryRetentionCountKey = "history_retention_count";
    private const string MaximumTextLengthKey = "maximum_text_length";
    private const string PauseRecordingUntilKey = "pause_recording_until";
    private const string PauseRecordingIndefinitelyKey = "pause_recording_indefinitely";

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        return await WithConnectionAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT key, value
                FROM settings;
                """;

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                values[reader.GetString(0)] = reader.GetString(1);
            }

            return AppSettings.Defaults with
            {
                PanelHotkey = GetString(values, PanelHotkeyKey, AppSettings.Defaults.PanelHotkey),
                HistoryRetentionCount = GetInt32(values, HistoryRetentionCountKey, AppSettings.Defaults.HistoryRetentionCount),
                MaximumTextLength = GetInt32(values, MaximumTextLengthKey, AppSettings.Defaults.MaximumTextLength),
                PauseRecordingUntil = GetNullableTimestamp(values, PauseRecordingUntilKey),
                PauseRecordingIndefinitely = GetBoolean(values, PauseRecordingIndefinitelyKey, AppSettings.Defaults.PauseRecordingIndefinitely)
            };
        }, cancellationToken);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await WithConnectionAsync(async connection =>
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            await SaveSettingAsync(connection, transaction, PanelHotkeyKey, settings.PanelHotkey, cancellationToken);
            await SaveSettingAsync(connection, transaction, HistoryRetentionCountKey, settings.HistoryRetentionCount.ToString(CultureInfo.InvariantCulture), cancellationToken);
            await SaveSettingAsync(connection, transaction, MaximumTextLengthKey, settings.MaximumTextLength.ToString(CultureInfo.InvariantCulture), cancellationToken);
            await SaveSettingAsync(connection, transaction, PauseRecordingUntilKey, FormatNullableTimestamp(settings.PauseRecordingUntil), cancellationToken);
            await SaveSettingAsync(connection, transaction, PauseRecordingIndefinitelyKey, settings.PauseRecordingIndefinitely.ToString(CultureInfo.InvariantCulture), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }, cancellationToken);
    }

    private async Task WithConnectionAsync(Func<SqliteConnection, Task> action, CancellationToken cancellationToken)
    {
        await WithConnectionAsync(async connection =>
        {
            await action(connection);
            return true;
        }, cancellationToken);
    }

    private async Task<T> WithConnectionAsync<T>(Func<SqliteConnection, Task<T>> action, CancellationToken cancellationToken)
    {
        var connection = createConnection();
        var shouldDisposeConnection = connection.State != ConnectionState.Open;

        try
        {
            if (shouldDisposeConnection)
            {
                await connection.OpenAsync(cancellationToken);
            }

            return await action(connection);
        }
        finally
        {
            if (shouldDisposeConnection)
            {
                await connection.DisposeAsync();
            }
        }
    }

    private static async Task SaveSettingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO settings (key, value)
            VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string GetString(IReadOnlyDictionary<string, string> values, string key, string defaultValue)
    {
        return values.TryGetValue(key, out var value) ? value : defaultValue;
    }

    private static int GetInt32(IReadOnlyDictionary<string, string> values, string key, int defaultValue)
    {
        return values.TryGetValue(key, out var value)
            ? int.Parse(value, CultureInfo.InvariantCulture)
            : defaultValue;
    }

    private static bool GetBoolean(IReadOnlyDictionary<string, string> values, string key, bool defaultValue)
    {
        return values.TryGetValue(key, out var value) ? bool.Parse(value) : defaultValue;
    }

    private static DateTimeOffset? GetNullableTimestamp(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value.Length == 0)
        {
            return AppSettings.Defaults.PauseRecordingUntil;
        }

        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string FormatNullableTimestamp(DateTimeOffset? value)
    {
        return value is null
            ? string.Empty
            : value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }
}
