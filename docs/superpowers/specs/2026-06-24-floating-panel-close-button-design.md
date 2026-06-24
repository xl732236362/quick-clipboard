# Floating Panel Close Button Design

## Context

Quick Clipboard uses a borderless WPF floating panel. The panel already closes through the window-owned `RequestClose()` path when the user presses `Esc`, the window deactivates, or the view model requests close during paste.

## Goal

Add a visible close button in the floating panel's upper-right corner so the user can dismiss the panel directly with the mouse.

## Design

- Add a small close button inside `FloatingPanelWindow.xaml`, aligned to the upper-right of the panel.
- Keep the existing borderless panel style.
- Route the click handler to `FloatingPanelWindow.RequestClose()` so the new button shares the same close-once guard as existing close paths.
- Keep the button compact enough that it does not crowd the tab headers or list content.
- Provide a `ToolTip` of `关闭`.

## Non-Goals

- Do not add a new view model command for this window-owned action.
- Do not restore the native Windows title bar.
- Do not change paste, focus-restore, or deactivation behavior.

## Verification

- Build and run the app test project.
- Manually open the panel and click the upper-right close button.
- Confirm `Esc` still closes the panel.
