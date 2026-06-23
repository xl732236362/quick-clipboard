using FluentAssertions;
using Microsoft.Data.Sqlite;
using QuickClipboard.Infrastructure.Persistence;

namespace QuickClipboard.Infrastructure.Tests;

public sealed class DatabaseInitializerTests
{
    [Fact]
    public async Task InitializeAsync_CreatesRequiredTables()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var initializer = new DatabaseInitializer(() => connection);

        await initializer.InitializeAsync();

        var tableNames = await ReadTableNames(connection);
        tableNames.Should().Contain(["clipboard_items", "favorites", "settings"]);
    }

    private static async Task<IReadOnlyList<string>> ReadTableNames(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
