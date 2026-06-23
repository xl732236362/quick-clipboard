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
            new FakeHotkeyInputGate(),
            new FakeForegroundWindowRestorer(),
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
            new FakeHotkeyInputGate(),
            new FakeForegroundWindowRestorer(),
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
            new FakeHotkeyInputGate(),
            new FakeForegroundWindowRestorer(),
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
        var gate = new FakeHotkeyInputGate();
        var controller = new FavoriteHotkeyController(
            new FakeHotkeyRegistrar(),
            repository,
            insertion,
            gate,
            new FakeForegroundWindowRestorer(),
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
        var gate = new FakeHotkeyInputGate();
        var controller = new FavoriteHotkeyController(
            new FakeHotkeyRegistrar(),
            repository,
            insertion,
            gate,
            new FakeForegroundWindowRestorer(),
            new FakeClock(Now));

        await controller.HandleHotkeyPressedAsync("panel");
        await controller.HandleHotkeyPressedAsync($"favorite:{Guid.NewGuid()}");

        insertion.InsertedText.Should().BeEmpty();
        repository.MarkedFavoriteIds.Should().BeEmpty();
        gate.WaitCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleFavoriteHotkeyPressedWaitsForInputGateBeforeInserting()
    {
        var favorite = CreateFavorite("Greeting", "hello", "Ctrl+Alt+1");
        var operations = new List<string>();
        var repository = new FakeClipboardRepository([favorite], operations);
        var insertion = new FakeTextInsertionService(operations);
        var gate = new FakeHotkeyInputGate(operations);
        var restorer = new FakeForegroundWindowRestorer(operations);
        var controller = new FavoriteHotkeyController(
            new FakeHotkeyRegistrar(),
            repository,
            insertion,
            gate,
            restorer,
            new FakeClock(Now));

        await controller.HandleHotkeyPressedAsync($"favorite:{favorite.Id}");

        gate.WaitCalls.Should().Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt);
        operations.Should().Equal("capture", "gate:Alt, Control", "restore:123", "insert:hello", "mark");
        repository.MarkedFavoriteIds.Should().Equal(favorite.Id);
    }

    [Fact]
    public async Task HandleFavoriteHotkeyPressedDoesNotRestoreOrInsertUnknownFavorite()
    {
        var favorite = CreateFavorite("Greeting", "hello", "Ctrl+Alt+1");
        var operations = new List<string>();
        var repository = new FakeClipboardRepository([favorite], operations);
        var insertion = new FakeTextInsertionService(operations);
        var gate = new FakeHotkeyInputGate(operations);
        var restorer = new FakeForegroundWindowRestorer(operations);
        var controller = new FavoriteHotkeyController(
            new FakeHotkeyRegistrar(),
            repository,
            insertion,
            gate,
            restorer,
            new FakeClock(Now));

        await controller.HandleHotkeyPressedAsync($"favorite:{Guid.NewGuid()}");

        operations.Should().Equal("capture");
        insertion.InsertedText.Should().BeEmpty();
        repository.MarkedFavoriteIds.Should().BeEmpty();
        gate.WaitCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleFavoriteHotkeyPressedSkipsInsertionWhenInputGateTimesOut()
    {
        var favorite = CreateFavorite("Greeting", "hello", "Ctrl+Alt+1");
        var repository = new FakeClipboardRepository([favorite]);
        var insertion = new FakeTextInsertionService();
        var gate = new FakeHotkeyInputGate
        {
            Released = false
        };
        var controller = new FavoriteHotkeyController(
            new FakeHotkeyRegistrar(),
            repository,
            insertion,
            gate,
            new FakeForegroundWindowRestorer(),
            new FakeClock(Now));

        await controller.HandleHotkeyPressedAsync($"favorite:{favorite.Id}");

        gate.WaitCalls.Should().Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt);
        insertion.InsertedText.Should().BeEmpty();
        repository.MarkedFavoriteIds.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleFavoriteHotkeyPressedSkipsInsertionWhenInputGateFails()
    {
        var favorite = CreateFavorite("Greeting", "hello", "Ctrl+Alt+1");
        var repository = new FakeClipboardRepository([favorite]);
        var insertion = new FakeTextInsertionService();
        var gate = new FakeHotkeyInputGate
        {
            Exception = new InvalidOperationException("gate failed")
        };
        var controller = new FavoriteHotkeyController(
            new FakeHotkeyRegistrar(),
            repository,
            insertion,
            gate,
            new FakeForegroundWindowRestorer(),
            new FakeClock(Now));

        await controller.HandleHotkeyPressedAsync($"favorite:{favorite.Id}");

        insertion.InsertedText.Should().BeEmpty();
        repository.MarkedFavoriteIds.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleFavoriteHotkeyPressedPropagatesCancellationFromInputGate()
    {
        var favorite = CreateFavorite("Greeting", "hello", "Ctrl+Alt+1");
        var repository = new FakeClipboardRepository([favorite]);
        var insertion = new FakeTextInsertionService();
        var gate = new FakeHotkeyInputGate
        {
            Exception = new OperationCanceledException("cancelled")
        };
        var controller = new FavoriteHotkeyController(
            new FakeHotkeyRegistrar(),
            repository,
            insertion,
            gate,
            new FakeForegroundWindowRestorer(),
            new FakeClock(Now));

        var act = () => controller.HandleHotkeyPressedAsync($"favorite:{favorite.Id}");

        await act.Should().ThrowAsync<OperationCanceledException>();
        insertion.InsertedText.Should().BeEmpty();
        repository.MarkedFavoriteIds.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshFavoriteHotkeysSerializesConcurrentRefreshes()
    {
        var first = CreateFavorite("First", "one", "Ctrl+Alt+1");
        var second = CreateFavorite("Second", "two", "Ctrl+Alt+2");
        var repository = new BlockingFavoritesRepository(
            firstFavorites: [first],
            secondFavorites: [second]);
        var hotkeys = new ObservingHotkeyRegistrar(repository);
        var controller = new FavoriteHotkeyController(
            hotkeys,
            repository,
            new FakeTextInsertionService(),
            new FakeHotkeyInputGate(),
            new FakeForegroundWindowRestorer(),
            new FakeClock(Now));

        var firstRefresh = controller.RefreshFavoriteHotkeysAsync();
        await repository.FirstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var secondRefresh = controller.RefreshFavoriteHotkeysAsync();

        repository.SecondReadStarted.Task.IsCompleted.Should().BeFalse();

        repository.AllowFirstRead.SetResult();
        await repository.SecondReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Task.WhenAll(firstRefresh, secondRefresh);

        hotkeys.RegisteredIds.Should().Equal($"favorite:{first.Id}", $"favorite:{second.Id}");
        hotkeys.UnregisteredIds.Should().Equal($"favorite:{first.Id}");
        hotkeys.ConcurrentRegistrarMutations.Should().Be(0);
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

    private class FakeHotkeyRegistrar : IGlobalHotkeyRegistrar
    {
        public string? FailedId { get; init; }
        public List<string> RegisteredIds { get; } = [];
        public List<string> RegisteredHotkeys { get; } = [];
        public List<string> UnregisteredIds { get; } = [];

        public virtual bool Register(string hotkeyId, Hotkey hotkey)
        {
            RegisteredIds.Add(hotkeyId);
            RegisteredHotkeys.Add(hotkey.ToString());
            return !string.Equals(hotkeyId, FailedId, StringComparison.Ordinal);
        }

        public virtual void Unregister(string hotkeyId)
        {
            UnregisteredIds.Add(hotkeyId);
        }
    }

    private class FakeClipboardRepository(
        IReadOnlyList<FavoriteItem> favorites,
        List<string>? operations = null) : IClipboardRepository
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

        public virtual Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(CancellationToken cancellationToken = default)
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
            operations?.Add("mark");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTextInsertionService : ITextInsertionService
    {
        private readonly List<string>? operations;

        public FakeTextInsertionService(List<string>? operations = null)
        {
            this.operations = operations;
        }

        public List<string> InsertedText { get; } = [];

        public Task InsertTextAsync(string text, CancellationToken cancellationToken = default)
        {
            InsertedText.Add(text);
            operations?.Add($"insert:{text}");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHotkeyInputGate(List<string>? operations = null) : IHotkeyInputGate
    {
        public Exception? Exception { get; init; }
        public bool Released { get; init; } = true;
        public List<HotkeyModifiers> WaitCalls { get; } = [];

        public Task<bool> WaitForModifiersReleasedAsync(
            HotkeyModifiers modifiers,
            CancellationToken cancellationToken = default)
        {
            WaitCalls.Add(modifiers);
            operations?.Add($"gate:{modifiers}");

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Released);
        }
    }

    private sealed class FakeForegroundWindowRestorer(
        List<string>? operations = null,
        IntPtr? capturedWindow = null) : IForegroundWindowRestorer
    {
        public IntPtr CaptureCurrent()
        {
            operations?.Add("capture");
            return capturedWindow ?? new IntPtr(123);
        }

        public Task RestoreAsync(IntPtr windowHandle, CancellationToken cancellationToken = default)
        {
            operations?.Add($"restore:{windowHandle}");
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingFavoritesRepository : FakeClipboardRepository
    {
        private readonly IReadOnlyList<FavoriteItem> firstFavorites;
        private readonly IReadOnlyList<FavoriteItem> secondFavorites;
        private int getFavoritesCallCount;

        public BlockingFavoritesRepository(
            IReadOnlyList<FavoriteItem> firstFavorites,
            IReadOnlyList<FavoriteItem> secondFavorites)
            : base(firstFavorites)
        {
            this.firstFavorites = firstFavorites;
            this.secondFavorites = secondFavorites;
        }

        public TaskCompletionSource FirstReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowFirstRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref getFavoritesCallCount);
            if (call == 1)
            {
                FirstReadStarted.SetResult();
                await AllowFirstRead.Task.WaitAsync(cancellationToken);
                return firstFavorites;
            }

            SecondReadStarted.SetResult();
            return secondFavorites;
        }
    }

    private sealed class ObservingHotkeyRegistrar(BlockingFavoritesRepository repository) : FakeHotkeyRegistrar
    {
        public int ConcurrentRegistrarMutations { get; private set; }

        public override bool Register(string hotkeyId, Hotkey hotkey)
        {
            if (repository.FirstReadStarted.Task.IsCompleted && !repository.AllowFirstRead.Task.IsCompleted)
            {
                ConcurrentRegistrarMutations++;
            }

            return base.Register(hotkeyId, hotkey);
        }

        public override void Unregister(string hotkeyId)
        {
            if (repository.FirstReadStarted.Task.IsCompleted && !repository.AllowFirstRead.Task.IsCompleted)
            {
                ConcurrentRegistrarMutations++;
            }

            base.Unregister(hotkeyId);
        }
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }
}
