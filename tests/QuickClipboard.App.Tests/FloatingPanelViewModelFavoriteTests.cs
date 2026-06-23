using FluentAssertions;
using QuickClipboard.App.Presentation.ViewModels;
using QuickClipboard.Core.Models;
using QuickClipboard.Core.Services;

namespace QuickClipboard.App.Tests;

public sealed class FloatingPanelViewModelFavoriteTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-23T01:00:00Z");

    [Fact]
    public async Task StartNewFavoriteCreatesEditorAndCancelClearsIt()
    {
        var viewModel = CreateViewModel();

        viewModel.NewFavoriteCommand.Execute(null);

        viewModel.FavoriteEditor.Should().NotBeNull();
        viewModel.EditingFavorite.Should().BeNull();
        viewModel.FavoriteEditor!.Title.Should().BeEmpty();
        viewModel.FavoriteEditor.Content.Should().BeEmpty();

        await viewModel.CancelFavoriteEditCommand.ExecuteAsync(null);

        viewModel.FavoriteEditor.Should().BeNull();
        viewModel.EditingFavorite.Should().BeNull();
    }

    [Fact]
    public async Task SaveNewFavoriteAddsFavoriteAndRefreshesHotkeys()
    {
        var repository = new FakeClipboardRepository();
        var refreshCount = 0;
        var viewModel = CreateViewModel(repository);
        viewModel.FavoriteHotkeysChangedAsync = _ =>
        {
            refreshCount++;
            return Task.CompletedTask;
        };

        viewModel.NewFavoriteCommand.Execute(null);
        viewModel.FavoriteEditor!.Title = "Greeting";
        viewModel.FavoriteEditor.Content = "hello";
        viewModel.FavoriteEditor.Hotkey = "Ctrl+Alt+1";

        await viewModel.SaveFavoriteCommand.ExecuteAsync(null);

        repository.AddedFavorites.Should().ContainSingle();
        var saved = repository.AddedFavorites[0];
        saved.Title.Should().Be("Greeting");
        saved.Content.Should().Be("hello");
        saved.Hotkey.Should().Be("Ctrl+Alt+1");
        saved.CreatedAt.Should().Be(Now);
        saved.UpdatedAt.Should().Be(Now);
        viewModel.FavoriteItems.Should().ContainSingle(item => item.Id == saved.Id);
        viewModel.FavoriteEditor.Should().BeNull();
        refreshCount.Should().Be(1);
    }

    [Fact]
    public async Task EditFavoriteLoadsEditorAndSaveUpdatesFavorite()
    {
        var favorite = CreateFavorite("Original", "old", "Ctrl+Alt+1");
        var repository = new FakeClipboardRepository([favorite]);
        var viewModel = CreateViewModel(repository);
        var refreshCount = 0;
        viewModel.FavoriteHotkeysChangedAsync = _ =>
        {
            refreshCount++;
            return Task.CompletedTask;
        };
        await viewModel.RefreshCommand.ExecuteAsync(null);

        var item = viewModel.FavoriteItems.Single();
        viewModel.EditFavoriteCommand.Execute(item);
        viewModel.EditingFavorite.Should().BeSameAs(item);
        viewModel.FavoriteEditor!.Title = "Updated";
        viewModel.FavoriteEditor.Content = "new";
        viewModel.FavoriteEditor.Hotkey = string.Empty;

        await viewModel.SaveFavoriteCommand.ExecuteAsync(null);

        repository.UpdatedFavorites.Should().ContainSingle();
        var saved = repository.UpdatedFavorites[0];
        saved.Id.Should().Be(favorite.Id);
        saved.Title.Should().Be("Updated");
        saved.Content.Should().Be("new");
        saved.Hotkey.Should().BeNull();
        saved.CreatedAt.Should().Be(favorite.CreatedAt);
        saved.UpdatedAt.Should().Be(Now);
        viewModel.FavoriteItems.Should().ContainSingle(x => x.Title == "Updated" && x.Content == "new");
        viewModel.FavoriteEditor.Should().BeNull();
        refreshCount.Should().Be(1);
    }

    [Fact]
    public async Task SaveFavoriteDoesNothingWhenEditorIsInvalid()
    {
        var repository = new FakeClipboardRepository();
        var viewModel = CreateViewModel(repository);
        var refreshCount = 0;
        viewModel.FavoriteHotkeysChangedAsync = _ =>
        {
            refreshCount++;
            return Task.CompletedTask;
        };

        viewModel.NewFavoriteCommand.Execute(null);
        viewModel.FavoriteEditor!.Title = string.Empty;
        viewModel.FavoriteEditor.Content = "hello";

        await viewModel.SaveFavoriteCommand.ExecuteAsync(null);

        repository.AddedFavorites.Should().BeEmpty();
        viewModel.FavoriteEditor.Should().NotBeNull();
        refreshCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteFavoriteRefreshesHotkeys()
    {
        var favorite = CreateFavorite("Greeting", "hello", "Ctrl+Alt+1");
        var repository = new FakeClipboardRepository([favorite]);
        var viewModel = CreateViewModel(repository);
        var refreshCount = 0;
        viewModel.FavoriteHotkeysChangedAsync = _ =>
        {
            refreshCount++;
            return Task.CompletedTask;
        };
        await viewModel.RefreshCommand.ExecuteAsync(null);

        await viewModel.DeleteFavoriteCommand.ExecuteAsync(viewModel.FavoriteItems.Single());

        repository.DeletedFavoriteIds.Should().Equal(favorite.Id);
        viewModel.FavoriteItems.Should().BeEmpty();
        refreshCount.Should().Be(1);
    }

    [Fact]
    public async Task AddHistoryToFavoritesRefreshesHotkeys()
    {
        var repository = new FakeClipboardRepository();
        var viewModel = CreateViewModel(repository);
        var refreshCount = 0;
        viewModel.FavoriteHotkeysChangedAsync = _ =>
        {
            refreshCount++;
            return Task.CompletedTask;
        };
        var historyItem = new ClipboardItemViewModel(new ClipboardItem(
            Guid.NewGuid(),
            "history content",
            "hash",
            "text",
            Now,
            LastUsedAt: null,
            UseCount: 0,
            SourceApp: null));

        await viewModel.AddHistoryToFavoritesCommand.ExecuteAsync(historyItem);

        repository.AddedFavorites.Should().ContainSingle();
        refreshCount.Should().Be(1);
    }

    [Fact]
    public async Task AddHistoryToFavoritesUsesChineseDefaultTitleWhenContentHasNoText()
    {
        var repository = new FakeClipboardRepository();
        var viewModel = CreateViewModel(repository);
        var historyItem = new ClipboardItemViewModel(new ClipboardItem(
            Guid.NewGuid(),
            " \r\n\t ",
            "hash",
            "text",
            Now,
            LastUsedAt: null,
            UseCount: 0,
            SourceApp: null));

        await viewModel.AddHistoryToFavoritesCommand.ExecuteAsync(historyItem);

        repository.AddedFavorites.Should().ContainSingle();
        repository.AddedFavorites[0].Title.Should().Be("收藏");
    }

    private static FloatingPanelViewModel CreateViewModel(FakeClipboardRepository? repository = null)
    {
        return new FloatingPanelViewModel(
            repository ?? new FakeClipboardRepository(),
            new FakeSettingsRepository(),
            new FakeClock(Now),
            new FakeTextInsertionService());
    }

    private static FavoriteItem CreateFavorite(string title, string content, string? hotkey)
    {
        return new FavoriteItem(
            Guid.NewGuid(),
            title,
            content,
            hotkey,
            SortOrder: 1,
            CreatedAt: DateTimeOffset.Parse("2026-06-22T01:00:00Z"),
            UpdatedAt: DateTimeOffset.Parse("2026-06-22T01:00:00Z"),
            LastUsedAt: null,
            UseCount: 0);
    }

    private sealed class FakeClipboardRepository(IReadOnlyList<FavoriteItem>? favorites = null) : IClipboardRepository
    {
        private readonly List<FavoriteItem> favorites = favorites?.ToList() ?? [];

        public List<FavoriteItem> AddedFavorites { get; } = [];
        public List<FavoriteItem> UpdatedFavorites { get; } = [];
        public List<Guid> DeletedFavoriteIds { get; } = [];

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
            return Task.FromResult<IReadOnlyList<FavoriteItem>>(favorites);
        }

        public Task AddFavoriteAsync(FavoriteItem item, CancellationToken cancellationToken = default)
        {
            AddedFavorites.Add(item);
            favorites.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateFavoriteAsync(FavoriteItem item, CancellationToken cancellationToken = default)
        {
            UpdatedFavorites.Add(item);
            var index = favorites.FindIndex(favorite => favorite.Id == item.Id);
            if (index >= 0)
            {
                favorites[index] = item;
            }

            return Task.CompletedTask;
        }

        public Task DeleteFavoriteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeletedFavoriteIds.Add(id);
            favorites.RemoveAll(favorite => favorite.Id == id);
            return Task.CompletedTask;
        }

        public Task MarkFavoriteUsedAsync(Guid id, DateTimeOffset usedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AppSettings.Defaults);
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTextInsertionService : ITextInsertionService
    {
        public Task InsertTextAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
