# Quick Clipboard

Quick Clipboard is a Windows-native clipboard assistant. It runs in the system tray, records recent plain-text clipboard history locally, and opens a floating panel with `Ctrl+Alt+V`.

## MVP Features

- Plain-text clipboard history.
- Favorites for common snippets.
- Favorite global hotkeys.
- Floating history/favorites panel.
- Local SQLite storage.
- Sensitive-text filtering.
- Pause recording and clear history controls.

## Development

```powershell
dotnet restore QuickClipboard.sln
dotnet build QuickClipboard.sln
dotnet test QuickClipboard.sln
dotnet run --project src/QuickClipboard.App/QuickClipboard.App.csproj
```

Manual integration checks are listed in `docs/manual-test-checklist.md`.
