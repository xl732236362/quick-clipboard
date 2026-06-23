using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickClipboard.Core.Models;
using QuickClipboard.Core.Services;

namespace QuickClipboard.App.Presentation.ViewModels;

public sealed partial class FloatingPanelViewModel : ObservableObject
{
    private const int HistoryLimit = 200;
    private const int FavoriteTitleLimit = 60;

    private readonly IClipboardRepository clipboardRepository;
    private readonly ISettingsRepository settingsRepository;
    private readonly IClock clock;
    private readonly ITextInsertionService textInsertionService;

    public FloatingPanelViewModel(
        IClipboardRepository clipboardRepository,
        ISettingsRepository settingsRepository,
        IClock clock,
        ITextInsertionService textInsertionService)
    {
        this.clipboardRepository = clipboardRepository;
        this.settingsRepository = settingsRepository;
        this.clock = clock;
        this.textInsertionService = textInsertionService;

        HistoryItems.CollectionChanged += OnHistoryItemsChanged;
        FavoriteItems.CollectionChanged += OnFavoriteItemsChanged;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        PasteHistoryCommand = new AsyncRelayCommand<ClipboardItemViewModel>(PasteHistoryAsync);
        PasteFavoriteCommand = new AsyncRelayCommand<FavoriteItemViewModel>(PasteFavoriteAsync);
        AddHistoryToFavoritesCommand = new AsyncRelayCommand<ClipboardItemViewModel>(AddHistoryToFavoritesAsync);
        DeleteHistoryCommand = new AsyncRelayCommand<ClipboardItemViewModel>(DeleteHistoryAsync);
        ClearHistoryCommand = new AsyncRelayCommand(ClearHistoryAsync);
        DeleteFavoriteCommand = new AsyncRelayCommand<FavoriteItemViewModel>(DeleteFavoriteAsync);
        NewFavoriteCommand = new RelayCommand(StartNewFavorite);
        EditFavoriteCommand = new RelayCommand<FavoriteItemViewModel>(StartEditFavorite);
        SaveFavoriteCommand = new AsyncRelayCommand(SaveFavoriteAsync, CanSaveFavorite);
        CancelFavoriteEditCommand = new AsyncRelayCommand(CancelFavoriteEditAsync);
    }

    public ObservableCollection<ClipboardItemViewModel> HistoryItems { get; } = new();
    public ObservableCollection<FavoriteItemViewModel> FavoriteItems { get; } = new();
    public bool HasNoHistory => HistoryItems.Count == 0;
    public bool HasNoFavorites => FavoriteItems.Count == 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFavoriteCommand))]
    private FavoriteEditorViewModel? favoriteEditor;

    [ObservableProperty]
    private FavoriteItemViewModel? editingFavorite;

    [ObservableProperty]
    private bool isRecordingPaused;

    [ObservableProperty]
    private string recordingStatusText = string.Empty;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<ClipboardItemViewModel> PasteHistoryCommand { get; }
    public IAsyncRelayCommand<FavoriteItemViewModel> PasteFavoriteCommand { get; }
    public IAsyncRelayCommand<ClipboardItemViewModel> AddHistoryToFavoritesCommand { get; }
    public IAsyncRelayCommand<ClipboardItemViewModel> DeleteHistoryCommand { get; }
    public IAsyncRelayCommand ClearHistoryCommand { get; }
    public IAsyncRelayCommand<FavoriteItemViewModel> DeleteFavoriteCommand { get; }
    public IRelayCommand NewFavoriteCommand { get; }
    public IRelayCommand<FavoriteItemViewModel> EditFavoriteCommand { get; }
    public IAsyncRelayCommand SaveFavoriteCommand { get; }
    public IAsyncRelayCommand CancelFavoriteEditCommand { get; }
    public Func<CancellationToken, Task> RestorePasteTarget { get; set; } = _ => Task.CompletedTask;
    public Func<CancellationToken, Task> FavoriteHotkeysChangedAsync { get; set; } = _ => Task.CompletedTask;

    public event EventHandler? CloseRequested;

    private void OnHistoryItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasNoHistory));
    }

    private void OnFavoriteItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasNoFavorites));
    }

    partial void OnFavoriteEditorChanged(FavoriteEditorViewModel? oldValue, FavoriteEditorViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnFavoriteEditorPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnFavoriteEditorPropertyChanged;
        }
    }

    private void OnFavoriteEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FavoriteEditorViewModel.IsValid) or nameof(FavoriteEditorViewModel.ValidationMessage))
        {
            SaveFavoriteCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var historyItems = await clipboardRepository.GetRecentClipboardItemsAsync(HistoryLimit, cancellationToken);
        var favoriteItems = await clipboardRepository.GetFavoritesAsync(cancellationToken);
        var settings = await settingsRepository.LoadAsync(cancellationToken);

        HistoryItems.Clear();
        foreach (var item in historyItems)
        {
            HistoryItems.Add(new ClipboardItemViewModel(item));
        }

        FavoriteItems.Clear();
        foreach (var item in favoriteItems)
        {
            FavoriteItems.Add(new FavoriteItemViewModel(item));
        }

        UpdateRecordingStatus(settings);
    }

    private async Task PasteHistoryAsync(ClipboardItemViewModel? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await PreparePasteTargetAsync(cancellationToken);
        await textInsertionService.InsertTextAsync(item.Content, cancellationToken);
        await clipboardRepository.MarkClipboardItemUsedAsync(item.Id, clock.Now, cancellationToken);
    }

    private async Task PasteFavoriteAsync(FavoriteItemViewModel? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await PreparePasteTargetAsync(cancellationToken);
        await textInsertionService.InsertTextAsync(item.Content, cancellationToken);
        await clipboardRepository.MarkFavoriteUsedAsync(item.Id, clock.Now, cancellationToken);
    }

    private async Task AddHistoryToFavoritesAsync(ClipboardItemViewModel? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        var now = clock.Now;
        var favorite = new FavoriteItem(
            Guid.NewGuid(),
            CreateFavoriteTitle(item.Content),
            item.Content,
            Hotkey: null,
            SortOrder: GetNextFavoriteSortOrder(),
            CreatedAt: now,
            UpdatedAt: now,
            LastUsedAt: null,
            UseCount: 0);

        await clipboardRepository.AddFavoriteAsync(favorite, cancellationToken);
        FavoriteItems.Add(new FavoriteItemViewModel(favorite));
        await FavoriteHotkeysChangedAsync(cancellationToken);
    }

    private async Task DeleteHistoryAsync(ClipboardItemViewModel? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await clipboardRepository.DeleteClipboardItemAsync(item.Id, cancellationToken);
        HistoryItems.Remove(item);
    }

    private async Task ClearHistoryAsync(CancellationToken cancellationToken)
    {
        await clipboardRepository.ClearClipboardItemsAsync(cancellationToken);
        HistoryItems.Clear();
    }

    private async Task DeleteFavoriteAsync(FavoriteItemViewModel? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await clipboardRepository.DeleteFavoriteAsync(item.Id, cancellationToken);
        FavoriteItems.Remove(item);
        if (ReferenceEquals(EditingFavorite, item))
        {
            ClearFavoriteEditor();
        }

        await FavoriteHotkeysChangedAsync(cancellationToken);
    }

    private void StartNewFavorite()
    {
        EditingFavorite = null;
        FavoriteEditor = new FavoriteEditorViewModel();
    }

    private void StartEditFavorite(FavoriteItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        EditingFavorite = item;
        FavoriteEditor = new FavoriteEditorViewModel
        {
            Title = item.Title,
            Content = item.Content,
            Hotkey = item.Hotkey
        };
    }

    private bool CanSaveFavorite()
    {
        return FavoriteEditor?.IsValid == true;
    }

    private async Task SaveFavoriteAsync(CancellationToken cancellationToken)
    {
        if (FavoriteEditor?.IsValid != true)
        {
            return;
        }

        if (EditingFavorite is null)
        {
            await SaveNewFavoriteAsync(FavoriteEditor, cancellationToken);
        }
        else
        {
            await SaveEditedFavoriteAsync(EditingFavorite, FavoriteEditor, cancellationToken);
        }

        ClearFavoriteEditor();
        await FavoriteHotkeysChangedAsync(cancellationToken);
    }

    private Task CancelFavoriteEditAsync(CancellationToken cancellationToken)
    {
        ClearFavoriteEditor();
        return Task.CompletedTask;
    }

    private async Task SaveNewFavoriteAsync(FavoriteEditorViewModel editor, CancellationToken cancellationToken)
    {
        var now = clock.Now;
        var favorite = new FavoriteItem(
            Guid.NewGuid(),
            editor.Title.Trim(),
            editor.Content,
            NormalizeHotkey(editor.Hotkey),
            SortOrder: GetNextFavoriteSortOrder(),
            CreatedAt: now,
            UpdatedAt: now,
            LastUsedAt: null,
            UseCount: 0);

        await clipboardRepository.AddFavoriteAsync(favorite, cancellationToken);
        FavoriteItems.Add(new FavoriteItemViewModel(favorite));
    }

    private async Task SaveEditedFavoriteAsync(
        FavoriteItemViewModel item,
        FavoriteEditorViewModel editor,
        CancellationToken cancellationToken)
    {
        var favorite = item.Item with
        {
            Title = editor.Title.Trim(),
            Content = editor.Content,
            Hotkey = NormalizeHotkey(editor.Hotkey),
            UpdatedAt = clock.Now
        };

        await clipboardRepository.UpdateFavoriteAsync(favorite, cancellationToken);
        var index = FavoriteItems.IndexOf(item);
        if (index >= 0)
        {
            FavoriteItems[index] = new FavoriteItemViewModel(favorite);
        }
    }

    private void ClearFavoriteEditor()
    {
        FavoriteEditor = null;
        EditingFavorite = null;
    }

    private int GetNextFavoriteSortOrder()
    {
        return FavoriteItems.Count == 0 ? 1 : FavoriteItems.Max(item => item.SortOrder) + 1;
    }

    private void UpdateRecordingStatus(AppSettings settings)
    {
        IsRecordingPaused = settings.PauseRecordingIndefinitely
            || (settings.PauseRecordingUntil is not null && settings.PauseRecordingUntil > clock.Now);
        RecordingStatusText = IsRecordingPaused ? "Recording paused" : string.Empty;
    }

    private async Task PreparePasteTargetAsync(CancellationToken cancellationToken)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        await RestorePasteTarget(cancellationToken);
    }

    private static string CreateFavoriteTitle(string content)
    {
        var title = content
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.None)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);

        title = string.IsNullOrEmpty(title) ? "Favorite" : title;
        return title.Length <= FavoriteTitleLimit ? title : title[..FavoriteTitleLimit];
    }

    private static string? NormalizeHotkey(string? hotkey)
    {
        return string.IsNullOrWhiteSpace(hotkey) ? null : hotkey.Trim();
    }
}
