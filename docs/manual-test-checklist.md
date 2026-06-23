# Quick Clipboard Manual Test Checklist

## Startup

- [ ] App starts without showing a main window.
- [ ] Tray icon appears.
- [ ] Tray Exit closes the process.

## Clipboard History

- [ ] Copy normal text from Notepad; it appears in history.
- [ ] Copy the same text twice; only one immediate history item appears.
- [ ] Copy `123456`; it is not recorded.
- [ ] Copy `password=my-secret-value`; it is not recorded.
- [ ] Clear history removes all history items.

## Floating Panel

- [ ] `Ctrl+Alt+V` opens the panel.
- [ ] Panel opens near caret in Notepad.
- [ ] Panel falls back to mouse position when caret is unavailable.
- [ ] Panel stays inside screen edges.
- [ ] `Esc` closes the panel.
- [ ] Clicking outside closes the panel.

## Insertion

- [ ] Clicking a history item inserts text into Notepad.
- [ ] Clicking a favorite item inserts text into Notepad.
- [ ] Previous clipboard content is restored after insertion.
- [ ] Unicode and Chinese text paste correctly.

## Favorites

- [ ] History item can be added to favorites.
- [ ] Favorite can be created manually.
- [ ] Favorite can be edited.
- [ ] Favorite can be deleted.
- [ ] Favorite hotkey inserts content without opening the panel.
- [ ] Deleted favorite hotkey no longer inserts content.

## Pause Recording

- [ ] Pause for 10 minutes blocks new history.
- [ ] Pause for 1 hour blocks new history.
- [ ] Pause until resumed blocks new history after restart.
- [ ] Resume recording allows new history.

## Compatibility

- [ ] Browser input field insertion works.
- [ ] VS Code editor insertion works.
- [ ] Common chat app insertion works.
- [ ] Behavior is acceptable with Chinese input method enabled.
- [ ] Elevated apps either work or fail without crashing.
- [ ] Multi-monitor positioning is acceptable.
