using QuickClipboard.Core.Models;

namespace QuickClipboard.Core.Services;

public interface IClipboardRepository
{
    Task<string?> GetLatestClipboardHashAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClipboardItem>> GetRecentClipboardItemsAsync(int limit, CancellationToken cancellationToken = default);
    Task AddClipboardItemAsync(ClipboardItem item, int retentionLimit, CancellationToken cancellationToken = default);
    Task DeleteClipboardItemAsync(Guid id, CancellationToken cancellationToken = default);
    Task ClearClipboardItemsAsync(CancellationToken cancellationToken = default);
    Task MarkClipboardItemUsedAsync(Guid id, DateTimeOffset usedAt, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(CancellationToken cancellationToken = default);
    Task AddFavoriteAsync(FavoriteItem item, CancellationToken cancellationToken = default);
    Task UpdateFavoriteAsync(FavoriteItem item, CancellationToken cancellationToken = default);
    Task DeleteFavoriteAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkFavoriteUsedAsync(Guid id, DateTimeOffset usedAt, CancellationToken cancellationToken = default);
}
