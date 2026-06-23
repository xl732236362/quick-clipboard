namespace QuickClipboard.Core.Favorites;

public sealed record FavoriteHotkeyStatus(Guid FavoriteId, string Hotkey, bool IsRegistered, string? Error);
