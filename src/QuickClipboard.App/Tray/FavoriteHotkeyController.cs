using System.Diagnostics;
using QuickClipboard.Core.Hotkeys;
using QuickClipboard.Core.Services;

namespace QuickClipboard.App.Tray;

public sealed class FavoriteHotkeyController(
    IGlobalHotkeyRegistrar hotkeyRegistrar,
    IClipboardRepository clipboardRepository,
    ITextInsertionService textInsertionService,
    IHotkeyInputGate hotkeyInputGate,
    IClock clock)
{
    private const string FavoriteHotkeyPrefix = "favorite:";

    private readonly HashSet<string> registeredFavoriteHotkeyIds = [];
    private readonly SemaphoreSlim refreshLock = new(1, 1);

    public async Task RefreshFavoriteHotkeysAsync(CancellationToken cancellationToken = default)
    {
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var hotkeyId in registeredFavoriteHotkeyIds.ToArray())
            {
                hotkeyRegistrar.Unregister(hotkeyId);
                registeredFavoriteHotkeyIds.Remove(hotkeyId);
            }

            var favorites = await clipboardRepository.GetFavoritesAsync(cancellationToken);
            foreach (var favorite in favorites)
            {
                if (string.IsNullOrWhiteSpace(favorite.Hotkey))
                {
                    continue;
                }

                if (!Hotkey.TryParse(favorite.Hotkey, out var hotkey) || hotkey is null)
                {
                    Debug.WriteLine($"Favorite hotkey '{favorite.Hotkey}' for '{favorite.Id}' could not be parsed.");
                    continue;
                }

                var hotkeyId = CreateFavoriteHotkeyId(favorite.Id);
                try
                {
                    if (!hotkeyRegistrar.Register(hotkeyId, hotkey))
                    {
                        Debug.WriteLine($"Favorite hotkey '{favorite.Hotkey}' for '{favorite.Id}' could not be registered.");
                        continue;
                    }

                    registeredFavoriteHotkeyIds.Add(hotkeyId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Favorite hotkey registration failed for '{favorite.Id}': {ex}");
                }
            }
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public async Task HandleHotkeyPressedAsync(string hotkeyId, CancellationToken cancellationToken = default)
    {
        if (!TryParseFavoriteHotkeyId(hotkeyId, out var favoriteId))
        {
            return;
        }

        var favorites = await clipboardRepository.GetFavoritesAsync(cancellationToken);
        var favorite = favorites.FirstOrDefault(item => item.Id == favoriteId);
        if (favorite is null)
        {
            return;
        }

        if (Hotkey.TryParse(favorite.Hotkey, out var hotkey) && hotkey is not null)
        {
            bool modifiersReleased;
            try
            {
                modifiersReleased = await hotkeyInputGate.WaitForModifiersReleasedAsync(hotkey.Modifiers, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Favorite hotkey input gate failed for '{favorite.Id}': {ex}");
                return;
            }

            if (!modifiersReleased)
            {
                Debug.WriteLine($"Favorite hotkey input gate timed out for '{favorite.Id}'.");
                return;
            }
        }

        await textInsertionService.InsertTextAsync(favorite.Content, cancellationToken);
        await clipboardRepository.MarkFavoriteUsedAsync(favorite.Id, clock.Now, cancellationToken);
    }

    public bool IsFavoriteHotkeyId(string hotkeyId)
    {
        return TryParseFavoriteHotkeyId(hotkeyId, out _);
    }

    private static string CreateFavoriteHotkeyId(Guid favoriteId)
    {
        return $"{FavoriteHotkeyPrefix}{favoriteId}";
    }

    private static bool TryParseFavoriteHotkeyId(string hotkeyId, out Guid favoriteId)
    {
        favoriteId = Guid.Empty;
        return hotkeyId.StartsWith(FavoriteHotkeyPrefix, StringComparison.Ordinal)
            && Guid.TryParse(hotkeyId[FavoriteHotkeyPrefix.Length..], out favoriteId);
    }
}
