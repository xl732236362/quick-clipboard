namespace QuickClipboard.Core.Models;

public sealed record FavoriteItem(
    Guid Id,
    string Title,
    string Content,
    string? Hotkey,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastUsedAt,
    int UseCount);
