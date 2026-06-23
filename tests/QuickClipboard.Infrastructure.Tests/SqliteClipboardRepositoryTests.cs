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
