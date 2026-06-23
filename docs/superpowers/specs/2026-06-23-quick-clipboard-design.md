# Quick Clipboard Design

## Summary

Quick Clipboard is a Windows-native clipboard assistant inspired by the Windows 11 clipboard panel. It runs as a tray-resident WPF application and lets users call up a floating panel near the current text caret with a global hotkey. The panel shows text clipboard history and user-managed favorite snippets. Users can click an item to insert it into the active input field, or assign global hotkeys to favorite snippets for direct insertion.

The first version focuses on reliable daily use for plain text. It intentionally excludes images, rich text, cloud sync, database encryption, and advanced animation polish.

## Confirmed Direction

- Platform: Windows desktop.
- Technology: C#/.NET, WPF, Win32 interop where needed.
- Application shape: single-process tray-resident app.
- Clipboard content: plain text only in the first version.
- Insertion method: temporarily set the system clipboard, simulate `Ctrl+V`, then attempt to restore the previous clipboard content.
- Panel hotkey: default `Ctrl+Alt+V`, configurable later.
- Panel position: prefer active text caret position; fall back to mouse position.
- Panel layout: two top-level tabs, `History` and `Favorites`.
- Favorite hotkeys: only favorite items can have global direct-insert hotkeys.
- Storage: local SQLite database.
- Privacy: filter likely sensitive text and provide pause-recording actions.

## Goals

1. Record recent plain-text clipboard items locally.
2. Show a lightweight floating panel near the current typing location.
3. Insert a selected history or favorite item into the active input field.
4. Let users promote history items to favorites.
5. Let users create, edit, delete, and insert favorite snippets.
6. Let users assign global hotkeys to favorite snippets.
7. Avoid storing likely sensitive content by default.
8. Keep the first implementation simple enough to ship and iterate.

## Non-Goals

- Image, file, HTML, or rich-text clipboard history.
- Cloud sync or multi-device sync.
- Encrypted database storage.
- OCR, screenshot capture, or AI-assisted content features.
- Plugin system.
- Complex drag-and-drop sorting.
- Pixel-perfect Windows 11 clone behavior or animations.

## Architecture

The first version should be a single WPF process with clear service boundaries. WPF owns UI, view models, and tray interactions. Win32 and UI Automation calls are contained inside infrastructure services so low-level code does not leak across the app.

Suggested project areas:

- `App`: startup, dependency registration, lifecycle, tray icon.
- `Domain`: clipboard item, favorite item, settings, hotkey models, filtering policies.
- `Infrastructure`: SQLite repositories, Win32 hotkey registration, clipboard monitoring, caret positioning, text insertion.
- `Presentation`: WPF windows, controls, view models, commands.

Suggested services:

- `ClipboardMonitor`: listens for clipboard changes, accepts plain text, ignores duplicates, applies sensitivity filters, and suppresses app-generated clipboard changes.
- `ClipboardRepository`: persists history, favorites, and settings in SQLite.
- `HotkeyService`: registers the panel hotkey and favorite-item hotkeys through Win32 APIs.
- `PanelPositionService`: finds the active text caret rectangle through UI Automation or Win32 APIs, with mouse-position fallback.
- `TextInsertionService`: saves the current clipboard, writes target text, sends `Ctrl+V`, and attempts to restore the previous clipboard content.
- `FloatingPanelViewModel`: manages tab state, item lists, selection, paste commands, favorite commands, deletion, and refresh.
- `SettingsService`: manages history limits, hotkey values, pause-recording state, and maximum text length.

## Core Flows

### Recording History

When the user copies text, `ClipboardMonitor` receives a clipboard update. It should:

1. Ignore changes caused by the app's own insertion flow.
2. Read plain text only.
3. Ignore empty text.
4. Ignore text above the configured maximum length.
5. Ignore text that matches likely sensitive patterns.
6. Ignore immediate duplicates using content hash comparison.
7. Store accepted text in `clipboard_items`.
8. Trim old history beyond the configured retention count.

The default history retention count is 200 items. The default maximum text length is 20,000 characters.

### Opening the Panel

When the user presses `Ctrl+Alt+V`, `HotkeyService` opens the floating panel. The app should:

1. Ask `PanelPositionService` for the active text caret rectangle.
2. Fall back to the mouse position if caret lookup fails.
3. Clamp the panel inside the active monitor work area.
4. Show the panel as a topmost, borderless, non-taskbar WPF window.
5. Default to the `History` tab.
6. Close on `Esc`, focus loss, or successful insertion.

### Inserting an Item

When the user clicks a history or favorite item, the app should:

1. Close or hide the floating panel.
2. Tell `ClipboardMonitor` to suppress app-generated clipboard changes briefly.
3. Save the current clipboard content when possible.
4. Set the system clipboard to the selected text.
5. Simulate `Ctrl+V` in the active window.
6. Wait briefly.
7. Attempt to restore the previous clipboard content.
8. Update `last_used_at` and `use_count`.

This insertion strategy is the default because it is broadly compatible with Windows input fields and preserves Unicode text better than simulated per-character typing.

### Managing Favorites

Users can add favorites in two ways:

1. Click `Add to Favorites` on a history item.
2. Create a new favorite directly in the `Favorites` tab.

Favorite items support:

- title;
- content;
- optional global hotkey;
- edit;
- delete;
- click-to-insert;
- usage metadata.

Favorites are never created automatically. This avoids accidental persistence of sensitive history items.

### Favorite Hotkeys

Only favorites can have direct-insert global hotkeys. When a favorite hotkey is pressed, the app runs the same insertion flow as a clicked item without opening the panel.

If a favorite hotkey conflicts with another app or cannot be registered, the favorite should remain visible but show that its hotkey is inactive. The user can choose a different shortcut later.

## Data Model

SQLite is stored locally, for example at `%AppData%\QuickClipboard\quick-clipboard.db`.

### `clipboard_items`

- `id`: primary key.
- `content`: text content.
- `content_hash`: hash used for duplicate detection.
- `content_type`: initially always `text`.
- `created_at`: insertion timestamp.
- `last_used_at`: nullable last paste timestamp.
- `use_count`: number of insertions.
- `source_app`: optional process or window title if cheaply available.

### `favorites`

- `id`: primary key.
- `title`: user-facing label.
- `content`: text content.
- `hotkey`: nullable serialized hotkey.
- `sort_order`: numeric ordering value.
- `created_at`: creation timestamp.
- `updated_at`: last edit timestamp.
- `last_used_at`: nullable last paste timestamp.
- `use_count`: number of insertions.

### `settings`

Simple key-value settings, including:

- panel hotkey;
- history retention count;
- maximum text length;
- pause-recording state and expiry;
- future UI preferences.

## Privacy And Filtering

The app should default to conservative filtering. It should not store text that appears to be:

- short verification codes;
- passwords or password-like key-value pairs;
- API keys, tokens, secrets, or private keys;
- credit-card-like numbers;
- other high-risk patterns added over time.

The tray menu should provide pause-recording actions:

- pause for 10 minutes;
- pause for 1 hour;
- pause until manually resumed.

The app should also provide a clear-history action from the panel or tray menu.

Sensitive-content filtering will have false positives and false negatives. The first version should prefer false positives over silently storing secrets.

## Known Limitations

- Some elevated applications may reject simulated input from a non-elevated app.
- Some secure fields, games, terminals, remote sessions, or custom controls may not expose caret geometry.
- Caret positioning may fail in some applications; mouse-position fallback is required.
- Clipboard restoration is best-effort. If another app changes the clipboard during insertion, restoration may not be exact.
- Global hotkeys can conflict with other software and must report registration failure clearly.

## MVP Scope

The first implementation includes:

- tray-resident startup and exit;
- plain-text clipboard history;
- duplicate filtering;
- likely-sensitive-text filtering;
- local SQLite persistence;
- default retention of 200 history items;
- `Ctrl+Alt+V` floating panel hotkey;
- `History` and `Favorites` tabs;
- history item paste, delete, and add-to-favorites;
- favorite create, edit, delete, paste;
- favorite global hotkeys;
- pause recording for 10 minutes, 1 hour, or until resumed;
- clear history;
- caret-first panel positioning with mouse fallback;
- temporary-clipboard insertion with best-effort restoration.

## Testing Strategy

Automated tests should cover:

- text normalization and duplicate detection;
- sensitive-content filter patterns;
- SQLite repository behavior;
- settings read/write behavior;
- hotkey parsing and conflict-state modeling;
- history retention trimming.

Manual integration testing should cover:

- Notepad;
- browser input fields;
- VS Code;
- common chat apps;
- Chinese input method enabled;
- administrator windows;
- multi-monitor edge positioning;
- hotkey conflicts;
- pause and resume recording;
- clear history;
- clipboard restoration after insertion.

## Open Decisions For Implementation Planning

These details can be finalized during the implementation plan:

- exact .NET target version;
- MVVM helper library or no helper library;
- tray icon library choice;
- SQLite migration approach;
- precise sensitive-filter regex set;
- first-pass visual styling level.
