# Floating Panel Close Button Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a visible upper-right close button to the floating clipboard panel.

**Architecture:** Keep the action window-owned. The XAML defines a compact button in the panel chrome, and the code-behind routes its click to the existing `RequestClose()` method so it shares the close-once guard with Esc, deactivation, and paste close behavior.

**Tech Stack:** C# 13, .NET 9 WPF, xUnit.

---

## File Structure

- Modify `src/QuickClipboard.App/Presentation/FloatingPanelWindow.xaml`
  - Add a local close-button style.
  - Add a right-aligned `X` button in the top row of the existing panel grid.
  - Move the tab control to the next row so the close button does not overlay tab content.
- Modify `src/QuickClipboard.App/Presentation/FloatingPanelWindow.xaml.cs`
  - Add an `OnCloseButtonClick` handler that calls `RequestClose()`.

---

### Task 1: Add The Button And Close Handler

**Files:**
- Modify: `src/QuickClipboard.App/Presentation/FloatingPanelWindow.xaml`
- Modify: `src/QuickClipboard.App/Presentation/FloatingPanelWindow.xaml.cs`

- [ ] **Step 1: Add a local close-button style**

In `FloatingPanelWindow.xaml`, add this style inside `<Window.Resources>` after `PanelEditorTextBoxStyle`:

```xml
<Style x:Key="PanelCloseButtonStyle" TargetType="{x:Type Button}">
    <Setter Property="Width" Value="28" />
    <Setter Property="Height" Value="28" />
    <Setter Property="Padding" Value="0" />
    <Setter Property="Foreground" Value="{StaticResource PanelMutedTextBrush}" />
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderBrush" Value="Transparent" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="ToolTip" Value="关闭" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="{x:Type Button}">
                <Border Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="4">
                    <ContentPresenter HorizontalAlignment="Center"
                                      VerticalAlignment="Center"
                                      RecognizesAccessKey="True" />
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter Property="Foreground" Value="{StaticResource PanelTextBrush}" />
                        <Setter Property="Background" Value="{StaticResource PanelItemHoverBrush}" />
                        <Setter Property="BorderBrush" Value="{StaticResource PanelAccentBrush}" />
                    </Trigger>
                    <Trigger Property="IsPressed" Value="True">
                        <Setter Property="Background" Value="#3D3D3D" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

- [ ] **Step 2: Add a top chrome row and close button**

In the root panel `Grid`, replace the row definitions with:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
    <RowDefinition Height="Auto" />
</Grid.RowDefinitions>
```

Add this close button before the `TabControl`:

```xml
<Button Grid.Row="0"
        HorizontalAlignment="Right"
        VerticalAlignment="Top"
        Margin="0,0,0,4"
        Click="OnCloseButtonClick"
        Content="X"
        Style="{StaticResource PanelCloseButtonStyle}" />
```

Change the `TabControl` row from `Grid.Row="0"` to `Grid.Row="1"`, and change the bottom status grid row from `Grid.Row="1"` to `Grid.Row="2"`.

- [ ] **Step 3: Add the click handler**

In `FloatingPanelWindow.xaml.cs`, add this method above `OnSourceInitialized`:

```csharp
private void OnCloseButtonClick(object sender, RoutedEventArgs e)
{
    e.Handled = true;
    RequestClose();
}
```

- [ ] **Step 4: Run the app test project**

Run:

```powershell
dotnet test tests/QuickClipboard.App.Tests/QuickClipboard.App.Tests.csproj
```

Expected result: App tests pass.

- [ ] **Step 5: Commit the implementation**

Run:

```powershell
git add src/QuickClipboard.App/Presentation/FloatingPanelWindow.xaml src/QuickClipboard.App/Presentation/FloatingPanelWindow.xaml.cs
git commit -m "feat: add floating panel close button"
```

Expected result: one implementation commit.

---

### Task 2: Final Verification

**Files:**
- Read: `docs/superpowers/specs/2026-06-24-floating-panel-close-button-design.md`

- [ ] **Step 1: Run the full test suite**

Run:

```powershell
dotnet test QuickClipboard.sln
```

Expected result: all test projects pass.

- [ ] **Step 2: Run a desktop smoke check**

Run the app:

```powershell
dotnet run --project src/QuickClipboard.App/QuickClipboard.App.csproj
```

Manual check:

1. Open the floating panel with `Ctrl+Alt+V`.
2. Confirm an `X` button appears in the upper-right corner.
3. Click the button and confirm the panel closes.
4. Open the panel again and confirm `Esc` still closes it.

- [ ] **Step 3: Confirm clean git state**

Run:

```powershell
git status --short --untracked-files=all
```

Expected result: no uncommitted changes.
