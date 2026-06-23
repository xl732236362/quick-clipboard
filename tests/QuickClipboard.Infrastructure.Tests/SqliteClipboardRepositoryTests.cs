using FluentAssertions;
using Microsoft.Data.Sqlite;
using QuickClipboard.Core.Models;
using QuickClipboard.Infrastructure.Persistence;

namespace QuickClipboard.Infrastructure.Tests;

public sealed class SqliteClipboardRepositoryTests
{
    [Fact]
    public async Task AddClipboardItemAsync_StoresItemsAndTrimsOldHistory()
    {
        await using var connection = await CreateInitializedConnectionAsync();
        var repository = new SqliteClipboardRepository(() => connection);

        for (var i = 0; i < 3; i++)
        {
            await repository.AddClipboardItemAsync(CreateClipboardItem($"item {i}", DateTimeOffset.UtcNow.AddMinutes(i)), retentionLimit: 2);
        }

        var items = await repository.GetRecentClipboardItemsAsync(10);

        items.Select(item => item.Content).Should().Equal("item 2", "item 1");
    }

    [Fact]
    public async Task GetRecentClipboardItemsAsync_OrdersByUtcInstantAcrossOffsets()
    {
        await using var connection = await CreateInitializedConnectionAsync();
        var repository = new SqliteClipboardRepository(() => connection);
        var olderInstant = DateTimeOffset.Parse("2026-06-23T12:00:00+08:00");
        var newerInstant = DateTimeOffset.Parse("2026-06-23T05:00:00+00:00");

        await repository.AddClipboardItemAsync(CreateClipboardItem("older", olderInstant), retentionLimit: 10);
        await repository.AddClipboardItemAsync(CreateClipboardItem("newer", newerInstant), retentionLimit: 10);

        var items = await repository.GetRecentClipboardItemsAsync(10);

        items.Select(item => item.Content).Should().Equal("newer", "older");
    }

    [Fact]
    public async Task GetLatestClipboardHashAsync_ReturnsNewestHashAcrossOffsets()
    {
        await using var connection = await CreateInitializedConnectionAsync();
        var repository = new SqliteClipboardRepository(() => connection);
        var olderInstant = DateTimeOffset.Parse("2026-06-23T12:00:00+08:00");
        var newerInstant = DateTimeOffset.Parse("2026-06-23T05:00:00+00:00");

        await repository.AddClipboardItemAsync(new ClipboardItem(Guid.NewGuid(), "older", "older-hash", "text", olderInstant, null, 0, null), retentionLimit: 10);
        await repository.AddClipboardItemAsync(new ClipboardItem(Guid.NewGuid(), "newer", "newer-hash", "text", newerInstant, null, 0, null), retentionLimit: 10);

        var hash = await repository.GetLatestClipboardHashAsync();

        hash.Should().Be("newer-hash");
    }

    [Fact]
    public async Task AddClipboardItemAsync_RejectsNegativeRetentionLimit()
    {
        await using var connection = await CreateInitializedConnectionAsync();
        var repository = new SqliteClipboardRepository(() => connection);

        var act = async () => await repository.AddClipboardItemAsync(CreateClipboardItem("item", DateTimeOffset.UtcNow), retentionLimit: -1);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Favorites_CanBeCreatedUpdatedDeletedAndMarkedUsed()
    {
        await using var connection = await CreateInitializedConnectionAsync();
        var repository = new SqliteClipboardRepository(() => connection);
        var now = DateTimeOffset.UtcNow;
        var favorite = new FavoriteItem(Guid.NewGuid(), "Email", "hello@example.com", "Ctrl+Alt+E", 10, now, now, null, 0);

        await repository.AddFavoriteAsync(favorite);
        await repository.UpdateFavoriteAsync(favorite with { Title = "Work Email", SortOrder = 1 });
        await repository.MarkFavoriteUsedAsync(favorite.Id, now.AddMinutes(1));

        var favorites = await repository.GetFavoritesAsync();
        favorites.Should().ContainSingle();
        favorites[0].Title.Should().Be("Work Email");
        favorites[0].UseCount.Should().Be(1);
        favorites[0].LastUsedAt.Should().Be(now.AddMinutes(1));

        await repository.DeleteFavoriteAsync(favorite.Id);
        (await repository.GetFavoritesAsync()).Should().BeEmpty();
    }

    private static ClipboardItem CreateClipboardItem(string content, DateTimeOffset createdAt)
    {
        return new ClipboardItem(Guid.NewGuid(), content, content, "text", createdAt, null, 0, null);
    }

    private static async Task<SqliteConnection> CreateInitializedConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await new DatabaseInitializer(() => connection).InitializeAsync();
        return connection;
    }
}
