using FluentAssertions;
using Microsoft.Data.Sqlite;
using QuickClipboard.Core.Models;
using QuickClipboard.Infrastructure.Persistence;

namespace QuickClipboard.Infrastructure.Tests;

public sealed class SqliteSettingsRepositoryTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefaultsWhenSettingsAreMissing()
    {
        await using var connection = await CreateInitializedConnectionAsync();
        var repository = new SqliteSettingsRepository(() => connection);

        var settings = await repository.LoadAsync();

        settings.Should().Be(AppSettings.Defaults);
    }

    [Fact]
    public async Task SaveAsync_PersistsSettings()
    {
        await using var connection = await CreateInitializedConnectionAsync();
        var repository = new SqliteSettingsRepository(() => connection);
        var expected = AppSettings.Defaults with
        {
            PanelHotkey = "Ctrl+Shift+Space",
            HistoryRetentionCount = 50,
            MaximumTextLength = 1000,
            PauseRecordingUntil = DateTimeOffset.Parse("2026-06-23T12:00:00Z"),
            PauseRecordingIndefinitely = true
        };

        await repository.SaveAsync(expected);
        var actual = await repository.LoadAsync();

        actual.Should().Be(expected);
    }

    private static async Task<SqliteConnection> CreateInitializedConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await new DatabaseInitializer(() => connection).InitializeAsync();
        return connection;
    }
}
