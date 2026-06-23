using FluentAssertions;
using QuickClipboard.App.Tray;
using QuickClipboard.Core.Hotkeys;
using QuickClipboard.Core.Models;
using QuickClipboard.Core.Services;

namespace QuickClipboard.App.Tests;

public sealed class FavoriteHotkeyControllerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-23T01:00:00Z");

    [Fact]
    public async Task RefreshRegistersOnlyValidNonblankFavoriteHotkeys()
    {
        var first = CreateFavorite("First", "one", "Ctrl+Alt+1");
        var blank = CreateFavorite("Blank", "blank", "");
        var invalid = CreateFavorite("Invalid", "bad", "Ctrl+Nope");
        var second = CreateFavorite("Second", "two", "Ctrl+Alt+2");
        var hotkeys = new FakeHotkeyRegistrar();
        var controller = new FavoriteHotkeyController(
            hotkeys,
            new FakeClipboardRepository([first, blank, invalid, second]),
            new FakeTextInsertionService(),
            new FakeClock(Now));

        await controller.RefreshFavoriteHotkeysAsync();

        hotkeys.RegisteredIds.Should().Equal($"favorite:{first.Id}", $"favorite:{second.Id}");
        hotkeys.RegisteredHotkeys.Should().Equal("Ctrl+Alt+1", "Ctrl+Alt+2");
    }

    [Fact]
    public async Task RefreshUnregistersPreviouslyRegisteredFavoriteHotkeys()
    {
        var first = CreateFavorite("First", "one", "Ctrl+Alt+1");
        var second = CreateFavorite("Second", "two", "Ctrl+Alt+2");
        var hotkeys = new FakeHotkeyRegistrar();
        var repository = new FakeClipboardRepository([first]);
        var controller = new FavoriteHotkeyController(
            hotkeys,
            repository,
            new FakeTextInsertionService(),
            new FakeClock(Now));
        await controller.RefreshFavoriteHotkeysAsync();
        repository.Favorites = [second];

        await controller.RefreshFavoriteHotkeysAsync();

        hotkeys.UnregisteredIds.Should().Equal($"favorite:{first.Id}");
        hotkeys.RegisteredIds.Should().Equal($"favorite:{first.Id}", $"favorite:{second.Id}");
    }

    [Fact]
    public async Task RefreshContinuesWhenFavoriteHotkeyRegistrationFails()
    {
        var first = CreateFavorite("First", "one", "Ctrl+Alt+1");
        var second = CreateFavorite("Second", "two", "Ctrl+Alt+2");
        var hotkeys = new FakeHotkeyRegistrar
        {
            FailedId = $"favorite:{first.Id}"
        };
        var controller = new FavoriteHotkeyController(
            hotkeys,
            new FakeClipboardRepository([first, second]),
            new FakeTextInsertionService(),
            new FakeClock(Now));

        await controller.RefreshFavoriteHotkeysAsync();

        hotkeys.RegisteredIds.Should().Equal($"favorite:{first.Id}", $"favorite:{second.Id}");
    }

    [Fact]
    public async Task HandleFavoriteHotkeyPressedInsertsMatchingFavoriteAndMarksUsed()
    {
        var favorite = CreateFavorite("Greeting", "hello", "Ctrl+Alt+1");
        var repository = new FakeClipboardRepository([favorite]);
        var insertion = new FakeTextInsertionService();
        var controller = new FavoriteHotkeyController(
            new FakeHotkeyRegistrar(),
            repository,
            insertion,
            new FakeClock(Now));

        await controller.HandleHotkeyPressedAsync($"favorite:{favorite.Id}");

        insertion.InsertedText.Should().Equal("hello");
        repository.MarkedFavoriteIds.Should().Equal(favorite.Id);
        repository.MarkedFavoriteTimes.Should().Equal(Now);
    }

    [Fact]
    public async Task HandleFavoriteHotkeyPressedIgnoresUnknownOrNonFavoriteIds()
    {
        var favorite = CreateFavorite("Greeting", "hello", "Ctrl+Alt+1");
        var repository = new FakeClipboardRepository([favorite]);
        var insertion = new FakeTextInsertionService();
        var controller = new FavoriteHotkeyController(
            new FakeHotkeyRegistrar(),
            repository,
            insertion,
            new FakeClock(Now));

        await controller.HandleHotkeyPressedAsync("panel");
        await controller.HandleHotkeyPressedAsync($"favorite:{Guid.NewGuid()}");

        insertion.InsertedText.Should().BeEmpty();
        repository.MarkedFavoriteIds.Should().BeEmpty();
    }

    private static FavoriteItem CreateFavorite(string title, string content, string? hotkey)
    {
        return new FavoriteItem(
            Guid.NewGuid(),
            title,
            content,
            hotkey,
            SortOrder: 1,
            CreatedAt: Now,
            UpdatedAt: Now,
            LastUsedAt: null,
            UseCount: 0);
    }

    private sealed class FakeHotkeyRegistrar : IGlobalHotkeyRegistrar
    {
        public string? FailedId { get; init; }
        public List<string> RegisteredIds { get; } = [];
        public List<string> RegisteredHotkeys { get; } = [];
        public List<string> UnregisteredIds { get; } = [];

        public bool Register(string hotkeyId, Hotkey hotkey)
        {
            RegisteredIds.Add(hotkeyId);
            RegisteredHotkeys.Add(hotkey.ToString());
            return !string.Equals(hotkeyId, FailedId, StringComparison.Ordinal);
        }

        public void Unregister(string hotkeyId)
        {
            UnregisteredIds.Add(hotkeyId);
        }
    }

    private sealed class FakeClipboardRepository(IReadOnlyList<FavoriteItem> favorites) : IClipboardRepository
    {
        public IReadOnlyList<FavoriteItem> Favorites { get; set; } = favorites;
        public List<Guid> MarkedFavoriteIds { get; } = [];
        public List<DateTimeOffset> MarkedFavoriteTimes { get; } = [];

        public Task<string?> GetLatestClipboardHashAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<IReadOnlyList<ClipboardItem>> GetRecentClipboardItemsAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ClipboardItem>>([]);
        }

        public Task AddClipboardItemAsync(
            ClipboardItem item,
            int retentionLimit,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteClipboardItemAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearClipboardItemsAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MarkClipboardItemUsedAsync(Guid id, DateTimeOffset usedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Favorites);
        }

        public Task AddFavoriteAsync(FavoriteItem item, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateFavoriteAsync(FavoriteItem item, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteFavoriteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MarkFavoriteUsedAsync(Guid id, DateTimeOffset usedAt, CancellationToken cancellationToken = default)
        {
            MarkedFavoriteIds.Add(id);
            MarkedFavoriteTimes.Add(usedAt);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTextInsertionService : ITextInsertionService
    {
        public List<string> InsertedText { get; } = [];

        public Task InsertTextAsync(string text, CancellationToken cancellationToken = default)
        {
            InsertedText.Add(text);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }
}
