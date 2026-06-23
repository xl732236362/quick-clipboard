using System.Collections.ObjectModel;
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
    private readonly IClock clock;
    private readonly ITextInsertionService textInsertionService;

    public FloatingPanelViewModel(
        IClipboardRepository clipboardRepository,
        IClock clock,
        ITextInsertionService textInsertionService)
    {
        this.clipboardRepository = clipboardRepository;
        this.clock = clock;
        this.textInsertionService = textInsertionService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        PasteHistoryCommand = new AsyncRelayCommand<ClipboardItemViewModel>(PasteHistoryAsync);
        PasteFavoriteCommand = new AsyncRelayCommand<FavoriteItemViewModel>(PasteFavoriteAsync);
        AddHistoryToFavoritesCommand = new AsyncRelayCommand<ClipboardItemViewModel>(AddHistoryToFavoritesAsync);
        DeleteHistoryCommand = new AsyncRelayCommand<ClipboardItemViewModel>(DeleteHistoryAsync);
        DeleteFavoriteCommand = new AsyncRelayCommand<FavoriteItemViewModel>(DeleteFavoriteAsync);
    }

    public ObservableCollection<ClipboardItemViewModel> HistoryItems { get; } = new();
    public ObservableCollection<FavoriteItemViewModel> FavoriteItems { get; } = new();
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<ClipboardItemViewModel> PasteHistoryCommand { get; }
    public IAsyncRelayCommand<FavoriteItemViewModel> PasteFavoriteCommand { get; }
    public IAsyncRelayCommand<ClipboardItemViewModel> AddHistoryToFavoritesCommand { get; }
    public IAsyncRelayCommand<ClipboardItemViewModel> DeleteHistoryCommand { get; }
    public IAsyncRelayCommand<FavoriteItemViewModel> DeleteFavoriteCommand { get; }

    public event EventHandler? CloseRequested;

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var historyItems = await clipboardRepository.GetRecentClipboardItemsAsync(HistoryLimit, cancellationToken);
        var favoriteItems = await clipboardRepository.GetFavoritesAsync(cancellationToken);

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
    }

    private async Task PasteHistoryAsync(ClipboardItemViewModel? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await textInsertionService.InsertTextAsync(item.Content, cancellationToken);
        await clipboardRepository.MarkClipboardItemUsedAsync(item.Id, clock.Now, cancellationToken);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task PasteFavoriteAsync(FavoriteItemViewModel? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await textInsertionService.InsertTextAsync(item.Content, cancellationToken);
        await clipboardRepository.MarkFavoriteUsedAsync(item.Id, clock.Now, cancellationToken);
        CloseRequested?.Invoke(this, EventArgs.Empty);
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

    private async Task DeleteFavoriteAsync(FavoriteItemViewModel? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await clipboardRepository.DeleteFavoriteAsync(item.Id, cancellationToken);
        FavoriteItems.Remove(item);
    }

    private int GetNextFavoriteSortOrder()
    {
        return FavoriteItems.Count == 0 ? 1 : FavoriteItems.Max(item => item.SortOrder) + 1;
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
}
