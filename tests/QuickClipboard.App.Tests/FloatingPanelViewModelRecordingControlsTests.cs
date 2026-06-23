using FluentAssertions;
using QuickClipboard.App.Presentation.ViewModels;
using QuickClipboard.Core.Models;
using QuickClipboard.Core.Services;

namespace QuickClipboard.App.Tests;

public sealed class FloatingPanelViewModelRecordingControlsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-23T01:00:00Z");

    [Fact]
    public async Task ClearHistoryClearsRepositoryAndVisibleHistoryItems()
    {
        var history = new[]
        {
            CreateClipboardItem("first"),
            CreateClipboardItem("second")
        };
        var repository = new FakeClipboardRepository(history);
        var viewModel = CreateViewModel(repository);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        await viewModel.ClearHistoryCommand.ExecuteAsync(null);

        repository.ClearHistoryCallCount.Should().Be(1);
        viewModel.HistoryItems.Should().BeEmpty();
        viewModel.HasNoHistory.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshShowsRecordingPausedWhenPausedIndefinitely()
    {
        var settings = AppSettings.Defaults with { PauseRecordingIndefinitely = true };
        var viewModel = CreateViewModel(settingsRepository: new FakeSettingsRepository(settings));

        await viewModel.RefreshCommand.ExecuteAsync(null);

        viewModel.IsRecordingPaused.Should().BeTrue();
        viewModel.RecordingStatusText.Should().Be("记录已暂停");
    }

    [Fact]
    public async Task RefreshShowsRecordingPausedWhenPauseExpiresInFuture()
    {
        var settings = AppSettings.Defaults with { PauseRecordingUntil = Now.AddMinutes(10) };
        var viewModel = CreateViewModel(settingsRepository: new FakeSettingsRepository(settings));

        await viewModel.RefreshCommand.ExecuteAsync(null);

        viewModel.IsRecordingPaused.Should().BeTrue();
        viewModel.RecordingStatusText.Should().Be("记录已暂停");
    }

    [Fact]
    public async Task RefreshHidesRecordingPausedWhenPauseExpired()
    {
        var settings = AppSettings.Defaults with { PauseRecordingUntil = Now.AddMinutes(-1) };
        var viewModel = CreateViewModel(settingsRepository: new FakeSettingsRepository(settings));

        await viewModel.RefreshCommand.ExecuteAsync(null);

        viewModel.IsRecordingPaused.Should().BeFalse();
        viewModel.RecordingStatusText.Should().BeEmpty();
    }

    private static FloatingPanelViewModel CreateViewModel(
        FakeClipboardRepository? repository = null,
        FakeSettingsRepository? settingsRepository = null)
    {
        return new FloatingPanelViewModel(
            repository ?? new FakeClipboardRepository(),
            settingsRepository ?? new FakeSettingsRepository(AppSettings.Defaults),
            new FakeClock(Now),
            new FakeTextInsertionService());
    }

    private static ClipboardItem CreateClipboardItem(string content)
    {
        return new ClipboardItem(
            Guid.NewGuid(),
            content,
            $"hash-{content}",
            "text",
            Now,
            LastUsedAt: null,
            UseCount: 0,
            SourceApp: null);
    }

    private sealed class FakeClipboardRepository(IReadOnlyList<ClipboardItem>? history = null) : IClipboardRepository
    {
        private readonly List<ClipboardItem> history = history?.ToList() ?? [];

        public int ClearHistoryCallCount { get; private set; }

        public Task<string?> GetLatestClipboardHashAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<IReadOnlyList<ClipboardItem>> GetRecentClipboardItemsAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ClipboardItem>>(history.Take(limit).ToList());
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
            ClearHistoryCallCount++;
            history.Clear();
            return Task.CompletedTask;
        }

        public Task MarkClipboardItemUsedAsync(Guid id, DateTimeOffset usedAt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<FavoriteItem>>([]);
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
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettingsRepository(AppSettings settings) : ISettingsRepository
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(settings);
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }

    private sealed class FakeTextInsertionService : ITextInsertionService
    {
        public Task InsertTextAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
