using Microsoft.Data.Sqlite;

namespace QuickClipboard.Infrastructure.Persistence;

public sealed class DatabaseInitializer(Func<SqliteConnection> createConnection)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var connection = createConnection();
        var shouldDisposeConnection = connection.State != System.Data.ConnectionState.Open;

        try
        {
            if (shouldDisposeConnection)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await ExecuteAsync(connection, """
                CREATE TABLE IF NOT EXISTS clipboard_items (
                    id TEXT PRIMARY KEY,
                    content TEXT NOT NULL,
                    content_hash TEXT NOT NULL,
                    content_type TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    last_used_at TEXT NULL,
                    use_count INTEGER NOT NULL DEFAULT 0,
                    source_app TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_clipboard_items_created_at
                    ON clipboard_items(created_at DESC);
                CREATE TABLE IF NOT EXISTS favorites (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    content TEXT NOT NULL,
                    hotkey TEXT NULL,
                    sort_order INTEGER NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    last_used_at TEXT NULL,
                    use_count INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS ix_favorites_sort_order
                    ON favorites(sort_order ASC);
                CREATE TABLE IF NOT EXISTS settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );
                """, cancellationToken);
        }
        finally
        {
            if (shouldDisposeConnection)
            {
                await connection.DisposeAsync();
            }
        }
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string commandText, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
