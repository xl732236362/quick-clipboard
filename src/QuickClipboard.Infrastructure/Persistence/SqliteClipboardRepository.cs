using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;
using QuickClipboard.Core.Models;
using QuickClipboard.Core.Services;

namespace QuickClipboard.Infrastructure.Persistence;

public sealed class SqliteClipboardRepository(Func<SqliteConnection> createConnection) : IClipboardRepository
{
    public async Task<string?> GetLatestClipboardHashAsync(CancellationToken cancellationToken = default)
    {
        return await WithConnectionAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT content_hash
                FROM clipboard_items
                ORDER BY created_at DESC
                LIMIT 1;
                """;

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result as string;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ClipboardItem>> GetRecentClipboardItemsAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await WithConnectionAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, content, content_hash, content_type, created_at, last_used_at, use_count, source_app
                FROM clipboard_items
                ORDER BY created_at DESC
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$limit", limit);

            var items = new List<ClipboardItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(ReadClipboardItem(reader));
            }

            return items;
        }, cancellationToken);
    }

    public async Task AddClipboardItemAsync(ClipboardItem item, int retentionLimit, CancellationToken cancellationToken = default)
    {
        await WithConnectionAsync(async connection =>
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = """
                INSERT INTO clipboard_items (
                    id, content, content_hash, content_type, created_at, last_used_at, use_count, source_app
                )
                VALUES (
                    $id, $content, $contentHash, $contentType, $createdAt, $lastUsedAt, $useCount, $sourceApp
                );
                """;
            AddClipboardParameters(insertCommand, item);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);

            await using var trimCommand = connection.CreateCommand();
            trimCommand.CommandText = """
                DELETE FROM clipboard_items
                WHERE id NOT IN (
                    SELECT id FROM clipboard_items
                    ORDER BY created_at DESC
                    LIMIT $limit
                );
                """;
            trimCommand.Parameters.AddWithValue("$limit", retentionLimit);
            await trimCommand.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task DeleteClipboardItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await ExecuteNonQueryAsync("DELETE FROM clipboard_items WHERE id = $id;", command => AddId(command, id), cancellationToken);
    }

    public async Task ClearClipboardItemsAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteNonQueryAsync("DELETE FROM clipboard_items;", _ => { }, cancellationToken);
    }

    public async Task MarkClipboardItemUsedAsync(Guid id, DateTimeOffset usedAt, CancellationToken cancellationToken = default)
    {
        await ExecuteNonQueryAsync("""
            UPDATE clipboard_items
            SET last_used_at = $lastUsedAt,
                use_count = use_count + 1
            WHERE id = $id;
            """, command =>
        {
            AddId(command, id);
            command.Parameters.AddWithValue("$lastUsedAt", FormatTimestamp(usedAt));
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        return await WithConnectionAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, title, content, hotkey, sort_order, created_at, updated_at, last_used_at, use_count
                FROM favorites
                ORDER BY sort_order ASC, created_at ASC;
                """;

            var items = new List<FavoriteItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(ReadFavoriteItem(reader));
            }

            return items;
        }, cancellationToken);
    }

    public async Task AddFavoriteAsync(FavoriteItem item, CancellationToken cancellationToken = default)
    {
        await WithConnectionAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO favorites (
                    id, title, content, hotkey, sort_order, created_at, updated_at, last_used_at, use_count
                )
                VALUES (
                    $id, $title, $content, $hotkey, $sortOrder, $createdAt, $updatedAt, $lastUsedAt, $useCount
                );
                """;
            AddFavoriteParameters(command, item);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task UpdateFavoriteAsync(FavoriteItem item, CancellationToken cancellationToken = default)
    {
        await WithConnectionAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE favorites
                SET title = $title,
                    content = $content,
                    hotkey = $hotkey,
                    sort_order = $sortOrder,
                    created_at = $createdAt,
                    updated_at = $updatedAt,
                    last_used_at = $lastUsedAt,
                    use_count = $useCount
                WHERE id = $id;
                """;
            AddFavoriteParameters(command, item);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task DeleteFavoriteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await ExecuteNonQueryAsync("DELETE FROM favorites WHERE id = $id;", command => AddId(command, id), cancellationToken);
    }

    public async Task MarkFavoriteUsedAsync(Guid id, DateTimeOffset usedAt, CancellationToken cancellationToken = default)
    {
        await ExecuteNonQueryAsync("""
            UPDATE favorites
            SET last_used_at = $lastUsedAt,
                use_count = use_count + 1
            WHERE id = $id;
            """, command =>
        {
            AddId(command, id);
            command.Parameters.AddWithValue("$lastUsedAt", FormatTimestamp(usedAt));
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

    private async Task ExecuteNonQueryAsync(
        string commandText,
        Action<SqliteCommand> configureCommand,
        CancellationToken cancellationToken)
    {
        await WithConnectionAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            configureCommand(command);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    private static void AddClipboardParameters(SqliteCommand command, ClipboardItem item)
    {
        command.Parameters.AddWithValue("$id", item.Id.ToString("D"));
        command.Parameters.AddWithValue("$content", item.Content);
        command.Parameters.AddWithValue("$contentHash", item.ContentHash);
        command.Parameters.AddWithValue("$contentType", item.ContentType);
        command.Parameters.AddWithValue("$createdAt", FormatTimestamp(item.CreatedAt));
        command.Parameters.AddWithValue("$lastUsedAt", FormatNullableTimestamp(item.LastUsedAt));
        command.Parameters.AddWithValue("$useCount", item.UseCount);
        command.Parameters.AddWithValue("$sourceApp", (object?)item.SourceApp ?? DBNull.Value);
    }

    private static void AddFavoriteParameters(SqliteCommand command, FavoriteItem item)
    {
        command.Parameters.AddWithValue("$id", item.Id.ToString("D"));
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$content", item.Content);
        command.Parameters.AddWithValue("$hotkey", (object?)item.Hotkey ?? DBNull.Value);
        command.Parameters.AddWithValue("$sortOrder", item.SortOrder);
        command.Parameters.AddWithValue("$createdAt", FormatTimestamp(item.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", FormatTimestamp(item.UpdatedAt));
        command.Parameters.AddWithValue("$lastUsedAt", FormatNullableTimestamp(item.LastUsedAt));
        command.Parameters.AddWithValue("$useCount", item.UseCount);
    }

    private static void AddId(SqliteCommand command, Guid id)
    {
        command.Parameters.AddWithValue("$id", id.ToString("D"));
    }

    private static ClipboardItem ReadClipboardItem(SqliteDataReader reader)
    {
        return new ClipboardItem(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            ParseTimestamp(reader.GetString(4)),
            GetNullableTimestamp(reader, 5),
            reader.GetInt32(6),
            GetNullableString(reader, 7));
    }

    private static FavoriteItem ReadFavoriteItem(SqliteDataReader reader)
    {
        return new FavoriteItem(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            GetNullableString(reader, 3),
            reader.GetInt32(4),
            ParseTimestamp(reader.GetString(5)),
            ParseTimestamp(reader.GetString(6)),
            GetNullableTimestamp(reader, 7),
            reader.GetInt32(8));
    }

    private static object FormatNullableTimestamp(DateTimeOffset? value)
    {
        return value is null ? DBNull.Value : FormatTimestamp(value.Value);
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset? GetNullableTimestamp(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : ParseTimestamp(reader.GetString(ordinal));
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string? GetNullableString(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
