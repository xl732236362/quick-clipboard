# Adaptive Panel Position Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the floating clipboard panel choose an adaptive position near the active caret or mouse anchor while staying inside the correct monitor working area and repositioning after content size changes.

**Architecture:** Keep the existing `PanelPositionService` as the owner of anchor lookup and screen-boundary placement. Add a deterministic internal placement helper so unit tests can cover candidate ordering without depending on the real desktop. Update `FloatingPanelWindow` to reposition after initial load, async refresh, and later size changes.

**Tech Stack:** C# 13, .NET 9 WPF, Windows Forms `Screen`, xUnit, FluentAssertions.

---

## File Structure

- Modify `src/QuickClipboard.Infrastructure/Windows/PanelPositionService.cs`
  - Add a panel gap constant.
  - Keep `GetPreferredAnchor` unchanged.
  - Make `ClampPanelTopLeft` resolve the current monitor working area, then delegate to an internal deterministic placement helper.
  - Add helper methods for candidate fitting and clamped fallback.
- Modify `tests/QuickClipboard.Infrastructure.Tests/PanelPositionServiceTests.cs`
  - Add deterministic tests for below, above, right, left, clamped, and oversized placement.
  - Keep one public-path test that uses the current primary screen working area.
- Modify `src/QuickClipboard.App/Presentation/FloatingPanelWindow.xaml.cs`
  - Reposition after `RefreshCommand` completes.
  - Reposition on `SizeChanged`.
  - Skip zero-size positioning and avoid repeated `Left`/`Top` assignments when the position has not changed.

No new project files, packages, or public service abstractions are needed.

---

### Task 1: Add Failing Deterministic Placement Tests

**Files:**
- Modify: `tests/QuickClipboard.Infrastructure.Tests/PanelPositionServiceTests.cs`

- [ ] **Step 1: Replace the current tests with deterministic adaptive-placement tests**

Replace the contents of `tests/QuickClipboard.Infrastructure.Tests/PanelPositionServiceTests.cs` with:

```csharp
using FluentAssertions;
using QuickClipboard.Infrastructure.Windows;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace QuickClipboard.Infrastructure.Tests;

public sealed class PanelPositionServiceTests
{
    [Fact]
    public void CalculateAdaptivePanelTopLeft_PlacesPanelBelowAnchorWhenItFits()
    {
        var workingArea = new Rect(0, 0, 500, 400);
        var anchor = new Rect(100, 100, 4, 16);
        var panelSize = new Size(200, 100);

        var topLeft = PanelPositionService.CalculateAdaptivePanelTopLeft(anchor, panelSize, workingArea);

        topLeft.Should().Be(new Point(100, 124));
    }

    [Fact]
    public void CalculateAdaptivePanelTopLeft_PlacesPanelAboveAnchorWhenBottomWouldOverflow()
    {
        var workingArea = new Rect(0, 0, 500, 400);
        var anchor = new Rect(100, 360, 4, 16);
        var panelSize = new Size(200, 100);

        var topLeft = PanelPositionService.CalculateAdaptivePanelTopLeft(anchor, panelSize, workingArea);

        topLeft.Should().Be(new Point(100, 252));
    }

    [Fact]
    public void CalculateAdaptivePanelTopLeft_PlacesPanelRightOfAnchorWhenVerticalCandidatesDoNotFit()
    {
        var workingArea = new Rect(0, 0, 600, 300);
        var anchor = new Rect(200, 40, 4, 100);
        var panelSize = new Size(160, 220);

        var topLeft = PanelPositionService.CalculateAdaptivePanelTopLeft(anchor, panelSize, workingArea);

        topLeft.Should().Be(new Point(212, 40));
    }

    [Fact]
    public void CalculateAdaptivePanelTopLeft_PlacesPanelLeftOfAnchorWhenRightWouldOverflow()
    {
        var workingArea = new Rect(0, 0, 380, 300);
        var anchor = new Rect(300, 40, 4, 100);
        var panelSize = new Size(160, 220);

        var topLeft = PanelPositionService.CalculateAdaptivePanelTopLeft(anchor, panelSize, workingArea);

        topLeft.Should().Be(new Point(132, 40));
    }

    [Fact]
    public void CalculateAdaptivePanelTopLeft_ClampsPreferredCandidateWhenNoCandidateFits()
    {
        var workingArea = new Rect(0, 0, 300, 200);
        var anchor = new Rect(250, 100, 4, 60);
        var panelSize = new Size(220, 180);

        var topLeft = PanelPositionService.CalculateAdaptivePanelTopLeft(anchor, panelSize, workingArea);

        topLeft.Should().Be(new Point(80, 20));
    }

    [Fact]
    public void CalculateAdaptivePanelTopLeft_UsesWorkingAreaStartWhenPanelIsOversized()
    {
        var workingArea = new Rect(10, 20, 200, 150);
        var anchor = new Rect(30, 50, 4, 16);
        var oversizedPanel = new Size(300, 200);

        var topLeft = PanelPositionService.CalculateAdaptivePanelTopLeft(anchor, oversizedPanel, workingArea);

        topLeft.Should().Be(new Point(10, 20));
    }

    [Fact]
    public void ClampPanelTopLeft_KeepsPanelInsidePrimaryWorkingArea()
    {
        var workingArea = GetPrimaryWorkingAreaInDeviceIndependentPixels();
        var service = new PanelPositionService();
        var anchor = new Rect(workingArea.Right + 100, workingArea.Bottom + 100, 4, 16);
        var panelSize = new Size(420, 300);

        var topLeft = service.ClampPanelTopLeft(anchor, panelSize);

        topLeft.X.Should().BeInRange(workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - panelSize.Width));
        topLeft.Y.Should().BeInRange(workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - panelSize.Height));
    }

    private static Rect GetPrimaryWorkingAreaInDeviceIndependentPixels()
    {
        var workingArea = Forms.Screen.PrimaryScreen!.WorkingArea;
        using var graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
        var scaleX = graphics.DpiX / 96;
        var scaleY = graphics.DpiY / 96;

        return new Rect(
            workingArea.Left / scaleX,
            workingArea.Top / scaleY,
            workingArea.Width / scaleX,
            workingArea.Height / scaleY);
    }
}
```

- [ ] **Step 2: Run the new placement tests and confirm they fail**

Run:

```powershell
dotnet test tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj --filter PanelPositionServiceTests
```

Expected result: build fails because `PanelPositionService.CalculateAdaptivePanelTopLeft` does not exist.

---

### Task 2: Implement Adaptive Placement In `PanelPositionService`

**Files:**
- Modify: `src/QuickClipboard.Infrastructure/Windows/PanelPositionService.cs`
- Test: `tests/QuickClipboard.Infrastructure.Tests/PanelPositionServiceTests.cs`

- [ ] **Step 1: Add the gap constant and delegate public placement to deterministic helper**

In `src/QuickClipboard.Infrastructure/Windows/PanelPositionService.cs`, change the constants near the top of the class to:

```csharp
private const double FallbackAnchorSize = 8;
private const double PanelGap = 8;
private const double DefaultDpi = 96;
private const int TextPattern2Id = 10024;
```

Replace the current `ClampPanelTopLeft` method with:

```csharp
public Point ClampPanelTopLeft(Rect anchor, Size panelSize)
{
    var workingArea = GetWorkingAreaForAnchor(anchor);
    return CalculateAdaptivePanelTopLeft(anchor, panelSize, workingArea);
}
```

- [ ] **Step 2: Add deterministic placement helper methods**

In `PanelPositionService`, add these methods below `ClampPanelTopLeft` and above `TryGetAutomationCaretAnchor`:

```csharp
internal static Point CalculateAdaptivePanelTopLeft(Rect anchor, Size panelSize, Rect workingArea)
{
    var below = new Point(anchor.Left, anchor.Bottom + PanelGap);
    var candidates = new[]
    {
        below,
        new Point(anchor.Left, anchor.Top - PanelGap - panelSize.Height),
        new Point(anchor.Right + PanelGap, anchor.Top),
        new Point(anchor.Left - PanelGap - panelSize.Width, anchor.Top)
    };

    foreach (var candidate in candidates)
    {
        if (PanelFits(candidate, panelSize, workingArea))
        {
            return candidate;
        }
    }

    return new Point(
        ClampToWorkingAxis(below.X, panelSize.Width, workingArea.Left, workingArea.Right),
        ClampToWorkingAxis(below.Y, panelSize.Height, workingArea.Top, workingArea.Bottom));
}

private static Rect GetWorkingAreaForAnchor(Rect anchor)
{
    var anchorPixels = DeviceIndependentPointToScreenPixels(new Point(anchor.Left, anchor.Top));
    var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)anchorPixels.X, (int)anchorPixels.Y));
    return ScreenPixelsToDeviceIndependentRect(
        screen.WorkingArea.Left,
        screen.WorkingArea.Top,
        screen.WorkingArea.Width,
        screen.WorkingArea.Height);
}

private static bool PanelFits(Point topLeft, Size panelSize, Rect workingArea)
{
    return topLeft.X >= workingArea.Left
        && topLeft.Y >= workingArea.Top
        && topLeft.X + panelSize.Width <= workingArea.Right
        && topLeft.Y + panelSize.Height <= workingArea.Bottom;
}
```

- [ ] **Step 3: Run the targeted placement tests**

Run:

```powershell
dotnet test tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj --filter PanelPositionServiceTests
```

Expected result: all `PanelPositionServiceTests` pass.

- [ ] **Step 4: Run the full infrastructure test project**

Run:

```powershell
dotnet test tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj
```

Expected result: infrastructure tests pass.

- [ ] **Step 5: Commit the adaptive placement implementation**

Run:

```powershell
git add src/QuickClipboard.Infrastructure/Windows/PanelPositionService.cs tests/QuickClipboard.Infrastructure.Tests/PanelPositionServiceTests.cs
git commit -m "feat: adapt floating panel placement"
```

Expected result: a commit is created with the service and test changes.

---

### Task 3: Reposition The WPF Panel After Size Changes

**Files:**
- Modify: `src/QuickClipboard.App/Presentation/FloatingPanelWindow.xaml.cs`

- [ ] **Step 1: Add positioning state**

In `FloatingPanelWindow`, add this field after `private bool isClosed;`:

```csharp
private bool isPositioning;
```

- [ ] **Step 2: Subscribe to `SizeChanged`**

In the constructor, add `SizeChanged += OnSizeChanged;` with the other event subscriptions:

```csharp
Loaded += OnLoaded;
SourceInitialized += OnSourceInitialized;
SizeChanged += OnSizeChanged;
KeyDown += OnKeyDown;
Deactivated += OnDeactivated;
Closed += OnClosed;
```

- [ ] **Step 3: Reposition after refresh completes**

Replace `OnLoaded` with:

```csharp
private async void OnLoaded(object sender, RoutedEventArgs e)
{
    PositionPanel();

    try
    {
        await viewModel.RefreshCommand.ExecuteAsync(null);
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"FloatingPanelWindow refresh failed: {ex}");
    }

    PositionPanel();
}
```

- [ ] **Step 4: Make `PositionPanel` ignore zero size and avoid repeated assignments**

Replace `PositionPanel` with:

```csharp
private void PositionPanel()
{
    if (isPositioning)
    {
        return;
    }

    isPositioning = true;
    try
    {
        Measure(new System.Windows.Size(Width, MaxHeight));
        var panelSize = new System.Windows.Size(
            ActualWidth > 0 ? ActualWidth : DesiredSize.Width,
            ActualHeight > 0 ? ActualHeight : DesiredSize.Height);
        if (panelSize.Width <= 0 || panelSize.Height <= 0)
        {
            return;
        }

        var topLeft = panelPositionService.ClampPanelTopLeft(anchor, panelSize);
        if (!AreClose(Left, topLeft.X))
        {
            Left = topLeft.X;
        }

        if (!AreClose(Top, topLeft.Y))
        {
            Top = topLeft.Y;
        }
    }
    finally
    {
        isPositioning = false;
    }
}
```

- [ ] **Step 5: Add `SizeChanged` and position comparison helpers**

Add these methods below `PositionPanel`:

```csharp
private void OnSizeChanged(object sender, SizeChangedEventArgs e)
{
    PositionPanel();
}

private static bool AreClose(double left, double right)
{
    return !double.IsNaN(left)
        && !double.IsNaN(right)
        && Math.Abs(left - right) < 0.5;
}
```

- [ ] **Step 6: Unsubscribe from `SizeChanged` on close**

In `OnClosed`, add `SizeChanged -= OnSizeChanged;` with the other event unsubscriptions:

```csharp
Loaded -= OnLoaded;
SourceInitialized -= OnSourceInitialized;
SizeChanged -= OnSizeChanged;
KeyDown -= OnKeyDown;
Deactivated -= OnDeactivated;
Closed -= OnClosed;
```

- [ ] **Step 7: Build and run app tests**

Run:

```powershell
dotnet test tests/QuickClipboard.App.Tests/QuickClipboard.App.Tests.csproj
```

Expected result: app tests pass. This project does not currently include an STA WPF window test harness, so lifecycle repositioning is verified by build coverage and the manual checks in Task 4.

- [ ] **Step 8: Commit the window lifecycle repositioning**

Run:

```powershell
git add src/QuickClipboard.App/Presentation/FloatingPanelWindow.xaml.cs
git commit -m "fix: reposition floating panel after resizing"
```

Expected result: a commit is created with the WPF window lifecycle change.

---

### Task 4: Full Verification And Manual Position Checks

**Files:**
- Read: `docs/manual-test-checklist.md`

- [ ] **Step 1: Run the full automated suite**

Run:

```powershell
dotnet test QuickClipboard.sln
```

Expected result: all test projects pass.

- [ ] **Step 2: Start the app for manual checks**

Run:

```powershell
dotnet run --project src/QuickClipboard.App/QuickClipboard.App.csproj
```

Expected result: the app starts without a normal main window and the tray application is available.

- [ ] **Step 3: Check caret-near placement**

Manual check:

1. Open Notepad.
2. Put the caret near the middle of the screen and press `Ctrl+Alt+V`.
3. Confirm the panel opens below the caret with a small gap and remains inside the screen.
4. Move the caret near the bottom of the screen and press `Ctrl+Alt+V`.
5. Confirm the panel opens above the caret.
6. Move the caret near cramped vertical space close to the right edge and press `Ctrl+Alt+V`.
7. Confirm the panel chooses a side position or clamps fully inside the working area.

- [ ] **Step 4: Check fallback and scaling behavior**

Manual check:

1. Open a browser text input and press `Ctrl+Alt+V`; confirm the panel appears near the input caret.
2. Open VS Code and press `Ctrl+Alt+V` in an editor; confirm the panel appears near the editor caret or falls back near the mouse.
3. Move the mouse to a visible location, focus a surface without a text caret, and press `Ctrl+Alt+V`; confirm the panel appears near the mouse.
4. On a secondary monitor, press `Ctrl+Alt+V` near several edges; confirm the panel stays inside that monitor's working area.
5. With Windows display scaling above 100%, repeat the Notepad middle and bottom-edge checks; confirm placement remains close to the caret.

- [ ] **Step 5: Check refresh-driven repositioning**

Manual check:

1. Copy enough text snippets to create visible history entries.
2. Press `Ctrl+Alt+V` with the caret near the bottom of the screen.
3. Confirm the panel opens above the caret and remains inside the working area after history finishes loading.
4. Switch between history and favorites while the panel is open.
5. Confirm the panel stays inside the working area as content height changes.

- [ ] **Step 6: Record the verification result**

Run:

```powershell
git status --short
```

Expected result: no uncommitted implementation changes remain unless a manual-check note was intentionally edited.

Report the automated test command and manual checks in the final response.
