using FluentAssertions;
using QuickClipboard.Infrastructure.Windows;
using Forms = System.Windows.Forms;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace QuickClipboard.Infrastructure.Tests;

public sealed class PanelPositionServiceTests
{
    [Fact]
    public void ClampPanelTopLeft_PlacesPanelBelowAnchorWhenItFits()
    {
        var workingArea = GetPrimaryWorkingAreaInDeviceIndependentPixels();
        var service = new PanelPositionService();
        var anchor = new Rect(workingArea.Left + 100, workingArea.Top + 100, 4, 16);
        var panelSize = new Size(420, 300);

        var topLeft = service.ClampPanelTopLeft(anchor, panelSize);

        topLeft.X.Should().Be(anchor.Left);
        topLeft.Y.Should().Be(anchor.Bottom);
    }

    [Fact]
    public void ClampPanelTopLeft_PlacesPanelAboveAnchorWhenBottomWouldOverflow()
    {
        var workingArea = GetPrimaryWorkingAreaInDeviceIndependentPixels();
        var service = new PanelPositionService();
        var panelSize = new Size(420, 300);
        var anchor = new Rect(workingArea.Left + 100, workingArea.Bottom - 20, 4, 16);

        var topLeft = service.ClampPanelTopLeft(anchor, panelSize);

        topLeft.X.Should().Be(anchor.Left);
        topLeft.Y.Should().Be(anchor.Top - panelSize.Height);
    }

    [Fact]
    public void ClampPanelTopLeft_ClampsPanelInsideWorkingArea()
    {
        var workingArea = GetPrimaryWorkingAreaInDeviceIndependentPixels();
        var service = new PanelPositionService();
        var anchor = new Rect(workingArea.Right + 100, workingArea.Bottom + 100, 4, 16);
        var panelSize = new Size(420, 300);

        var topLeft = service.ClampPanelTopLeft(anchor, panelSize);

        topLeft.X.Should().BeInRange(workingArea.Left, workingArea.Right - panelSize.Width);
        topLeft.Y.Should().BeInRange(workingArea.Top, workingArea.Bottom - panelSize.Height);
    }

    [Fact]
    public void ClampPanelTopLeft_UsesWorkingAreaTopLeftWhenPanelIsOversized()
    {
        var workingArea = GetPrimaryWorkingAreaInDeviceIndependentPixels();
        var service = new PanelPositionService();
        var anchor = new Rect(workingArea.Left + 100, workingArea.Top + 100, 4, 16);
        var oversizedPanel = new Size(workingArea.Width * 2, workingArea.Height * 2);

        var topLeft = service.ClampPanelTopLeft(anchor, oversizedPanel);

        topLeft.X.Should().Be(workingArea.Left);
        topLeft.Y.Should().Be(workingArea.Top);
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
