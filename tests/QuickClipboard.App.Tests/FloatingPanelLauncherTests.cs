using System.Windows;
using FluentAssertions;
using QuickClipboard.App.Presentation;
using QuickClipboard.App.Presentation.ViewModels;
using QuickClipboard.Core.Models;
using QuickClipboard.Core.Services;

namespace QuickClipboard.App.Tests;

public sealed class FloatingPanelLauncherTests
{
    [Fact]
    public void OpenClosesExistingWindowBeforeShowingNewWindow()
    {
        var viewModels = new Queue<FloatingPanelViewModel>([CreateViewModel(), CreateViewModel()]);
        var anchors = new Queue<Rect>([new Rect(10, 20, 1, 1), new Rect(30, 40, 1, 1)]);
        var windows = new List<FakeFloatingPanelWindow>();

        var launcher = new FloatingPanelLauncher(
            () => viewModels.Dequeue(),
            new FakePanelAnchorProvider(() => anchors.Dequeue()),
            new FakeForegroundWindowRestorer(),
            (viewModel, anchor) =>
            {
                var window = new FakeFloatingPanelWindow(anchor);
                windows.Add(window);
                return window;
            });

        launcher.Open();
        launcher.Open();

        windows.Should().HaveCount(2);
        windows[0].CloseCount.Should().Be(1);
        windows[0].ShowCount.Should().Be(1);
        windows[1].CloseCount.Should().Be(0);
        windows[1].ShowCount.Should().Be(1);
        windows[1].Anchor.Should().Be(new Rect(30, 40, 1, 1));
    }

    [Fact]
    public void OpenClearsClosedWindowBeforeShowingNewWindow()
    {
        var windows = new List<FakeFloatingPanelWindow>();

        var launcher = new FloatingPanelLauncher(
            CreateViewModel,
            new FakePanelAnchorProvider(() => new Rect(10, 20, 1, 1)),
            new FakeForegroundWindowRestorer(),
            (_, anchor) =>
            {
                var window = new FakeFloatingPanelWindow(anchor);
                windows.Add(window);
                return window;
            });

        launcher.Open();
        windows[0].RaiseClosed();
        launcher.Open();

        windows.Should().HaveCount(2);
        windows[0].CloseCount.Should().Be(0);
        windows[1].ShowCount.Should().Be(1);
    }

    [Fact]
    public async Task PasteHistoryCommandClosesAndRestoresTargetBeforeInsertingText()
    {
        var calls = new List<string>();
        var item = new ClipboardItem(
            Guid.NewGuid(),
            "content",
            "hash",
            "text/plain",
            DateTimeOffset.Parse("2026-06-23T00:00:00Z"),
            null,
            0,
            SourceApp: null);
        var viewModel = new FloatingPanelViewModel(
            new FakeClipboardRepository(calls),
            new FakeClock(DateTimeOffset.Parse("2026-06-23T01:00:00Z")),
            new FakeTextInsertionService(calls));
        viewModel.CloseRequested += (_, _) => calls.Add("close");
        viewModel.RestorePasteTarget = _ =>
        {
            calls.Add("restore");
            return Task.CompletedTask;
        };

        await viewModel.PasteHistoryCommand.ExecuteAsync(new ClipboardItemViewModel(item));

        calls.Should().ContainInOrder("close", "restore", "insert:content", "mark-history");
    }

    [Fact]
    public async Task OpenInjectsCapturedWindowTargetIntoViewModel()
    {
        var calls = new List<string>();
        var viewModel = CreateViewModel();
        var restorer = new FakeForegroundWindowRestorer(new IntPtr(456), calls);
        var launcher = new FloatingPanelLauncher(
            () => viewModel,
            new FakePanelAnchorProvider(() => new Rect(10, 20, 1, 1)),
            restorer,
            (_, anchor) => new FakeFloatingPanelWindow(anchor));

        launcher.Open();
        await viewModel.RestorePasteTarget(CancellationToken.None);

        calls.Should().Equal("capture", "restore:456");
    }

    private sealed class FakePanelAnchorProvider(Func<Rect> getPreferredAnchor) : IPanelAnchorProvider
    {
        public Rect GetPreferredAnchor()
        {
            return getPreferredAnchor();
        }
    }

    private sealed class FakeFloatingPanelWindow(Rect anchor) : IFloatingPanelWindow
    {
        public int CloseCount { get; private set; }
        public int ShowCount { get; private set; }
        public Rect Anchor { get; } = anchor;
        public event EventHandler? Closed;

        public void Close()
        {
            CloseCount++;
        }

        public void Show()
        {
            ShowCount++;
        }

        public void RaiseClosed()
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeClipboardRepository(List<string> calls) : IClipboardRepository
    {
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
            calls.Add("mark-history");
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

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }

    private sealed class FakeTextInsertionService(List<string> calls) : ITextInsertionService
    {
        public Task InsertTextAsync(string text, CancellationToken cancellationToken = default)
        {
            calls.Add($"insert:{text}");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeForegroundWindowRestorer(
        IntPtr? capturedWindow = null,
        List<string>? calls = null) : IForegroundWindowRestorer
    {
        public IntPtr CaptureCurrent()
        {
            calls?.Add("capture");
            return capturedWindow ?? new IntPtr(123);
        }

        public Task RestoreAsync(IntPtr windowHandle, CancellationToken cancellationToken = default)
        {
            calls?.Add($"restore:{windowHandle}");
            return Task.CompletedTask;
        }
    }

    private static FloatingPanelViewModel CreateViewModel()
    {
        var calls = new List<string>();
        return new FloatingPanelViewModel(
            new FakeClipboardRepository(calls),
            new FakeClock(DateTimeOffset.Parse("2026-06-23T01:00:00Z")),
            new FakeTextInsertionService(calls));
    }
}
