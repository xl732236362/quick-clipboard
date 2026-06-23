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
