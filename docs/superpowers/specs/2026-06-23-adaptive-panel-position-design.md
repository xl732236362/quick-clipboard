# Adaptive Panel Position Design

## Context

Quick Clipboard opens a WPF floating panel from the global hotkey. The current positioning flow already has the right shape:

- `PanelAnchorProvider` asks `PanelPositionService` for the preferred anchor.
- `PanelPositionService` tries UI Automation caret lookup, then Win32 GUI caret lookup, then the mouse position.
- `FloatingPanelWindow` measures the window on `Loaded`, calls `ClampPanelTopLeft`, and sets `Left` and `Top`.

The next improvement is to make the panel adapt more intelligently around the text caret, while staying correct after content height changes and on multi-monitor or scaled displays.

## Goals

- Keep the panel near the active text caret whenever a caret can be detected.
- Fall back to the mouse position when no caret rectangle is available.
- Keep the panel fully inside the working area of the monitor that contains the anchor.
- Recalculate the panel position after async content loading changes the actual height.
- Handle multi-monitor and DPI scaling consistently enough for WPF device-independent coordinates.
- Keep the change localized to the existing presentation and infrastructure positioning path.

## Non-Goals

- Do not redesign the floating panel UI.
- Do not replace the WPF `Window` with `Popup` or another host.
- Do not persist the panel's last position.
- Do not add user-configurable placement settings in this slice.

## Recommended Approach

Enhance the existing `PanelPositionService` instead of adding a new service. It already owns both caret discovery and screen-boundary clamping, and the current project does not need another abstraction layer yet.

The service should evolve from simple below-or-above clamping into adaptive placement:

1. Resolve the anchor rectangle.
2. Resolve the monitor working area for the anchor point.
3. Build ordered candidate positions around the anchor.
4. Pick the first candidate where the panel fits fully inside the working area.
5. If none fit, clamp the preferred candidate into the working area.

## Placement Behavior

Use a small gap, such as 8 device-independent pixels, between the caret anchor and the panel.

Candidate order:

1. Below the caret, left-aligned with the anchor.
2. Above the caret, left-aligned with the anchor.
3. To the right of the caret, top-aligned with the anchor.
4. To the left of the caret, top-aligned with the anchor.
5. Clamped below-caret fallback inside the working area.

This order keeps the common case predictable: the panel appears below the active input point. Near the bottom edge it moves above, and near cramped vertical space it can choose a side position before falling back to clamped placement.

If the panel is larger than the available working area on one or both axes, return the working area's top-left for that oversized axis. The existing window `MaxHeight` and list scrolling remain responsible for keeping content usable.

## DPI And Multi-Monitor Handling

The service should keep accepting and returning WPF device-independent coordinates.

For screen selection, convert the anchor point to screen pixels, then use `System.Windows.Forms.Screen.FromPoint`. For working area math, convert the selected monitor working area back into device-independent coordinates before evaluating candidates.

When a WPF `PresentationSource` is available, use its `CompositionTarget` transforms for DIP-to-device and device-to-DIP conversion. When no source is available, continue using system DPI as a fallback. This preserves current behavior while improving placement logic.

## Window Lifecycle

`FloatingPanelWindow` should reposition more than once:

- On `Loaded`, measure and position immediately so the panel appears in the right area.
- After `RefreshCommand` completes, position again because loaded history and favorites can change height.
- On `SizeChanged`, position again when content changes while the panel remains open.

The window should guard against unnecessary repeat positioning by ignoring zero sizes and by only setting `Left` and `Top` when the calculated point changed.

## Test Plan

Add focused unit tests for the pure placement behavior:

- Places below the anchor when it fits.
- Places above when below would overflow.
- Places to the right or left when vertical positions do not fit.
- Clamps inside the working area when no candidate fully fits.
- Handles oversized panels by using the working area start for oversized axes.

Keep existing integration-style tests around the real primary screen working area, but move most placement coverage to a deterministic helper or internal overload that accepts an explicit working area. This avoids requiring a real multi-monitor setup in CI.

Add or update app-level tests around `FloatingPanelWindow` only if the current test harness can observe repositioning without launching a fragile UI integration test. Otherwise, verify the lifecycle behavior with manual testing.

Manual checks:

- Open the panel in Notepad with the caret near the middle, bottom, left edge, and right edge of the screen.
- Open the panel in a browser input and VS Code editor.
- Open the panel on a secondary monitor if available.
- Check behavior with Windows display scaling above 100%.
- Confirm the panel moves again after history or favorites load.
