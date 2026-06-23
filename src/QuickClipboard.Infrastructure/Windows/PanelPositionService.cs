using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class PanelPositionService
{
    private const double FallbackAnchorSize = 8;

    public Rect GetPreferredAnchor()
    {
        if (TryGetAutomationCaretAnchor(out var automationAnchor))
        {
            return automationAnchor;
        }

        if (TryGetGuiThreadCaretAnchor(out var guiThreadAnchor))
        {
            return guiThreadAnchor;
        }

        var mousePosition = Forms.Control.MousePosition;
        return new Rect(mousePosition.X, mousePosition.Y, FallbackAnchorSize, FallbackAnchorSize);
    }

    public Point ClampPanelTopLeft(Rect anchor, Size panelSize)
    {
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)anchor.Left, (int)anchor.Top));
        var workingArea = screen.WorkingArea;

        var left = anchor.Left;
        var top = anchor.Bottom;

        if (top + panelSize.Height > workingArea.Bottom)
        {
            top = anchor.Top - panelSize.Height;
        }

        left = Math.Clamp(left, workingArea.Left, workingArea.Right - panelSize.Width);
        top = Math.Clamp(top, workingArea.Top, workingArea.Bottom - panelSize.Height);

        return new Point(left, top);
    }

    private static bool TryGetAutomationCaretAnchor(out Rect anchor)
    {
        anchor = Rect.Empty;

        try
        {
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement is null)
            {
                return false;
            }

            if (!focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)
                || pattern is not TextPattern textPattern)
            {
                return false;
            }

            var ranges = textPattern.GetSelection();
            foreach (var range in ranges)
            {
                foreach (var rectangle in range.GetBoundingRectangles())
                {
                    if (rectangle.Width > 0 && rectangle.Height > 0)
                    {
                        anchor = rectangle;
                        return true;
                    }
                }
            }
        }
        catch (ElementNotAvailableException ex)
        {
            Debug.WriteLine($"PanelPositionService UI Automation caret lookup failed: {ex}");
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"PanelPositionService UI Automation caret lookup failed: {ex}");
        }
        catch (COMException ex)
        {
            Debug.WriteLine($"PanelPositionService UI Automation caret lookup failed: {ex}");
        }

        return false;
    }

    private static bool TryGetGuiThreadCaretAnchor(out Rect anchor)
    {
        anchor = Rect.Empty;

        var foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        var threadId = NativeMethods.GetWindowThreadProcessId(foregroundWindow, out _);
        if (threadId == 0)
        {
            return false;
        }

        var info = new NativeMethods.GuiThreadInfo
        {
            cbSize = Marshal.SizeOf<NativeMethods.GuiThreadInfo>()
        };

        if (!NativeMethods.GetGUIThreadInfo(threadId, ref info) || info.hwndCaret == IntPtr.Zero)
        {
            return false;
        }

        var topLeft = new NativeMethods.NativePoint
        {
            X = info.rcCaret.Left,
            Y = info.rcCaret.Top
        };
        var bottomRight = new NativeMethods.NativePoint
        {
            X = info.rcCaret.Right,
            Y = info.rcCaret.Bottom
        };

        if (!NativeMethods.ClientToScreen(info.hwndCaret, ref topLeft)
            || !NativeMethods.ClientToScreen(info.hwndCaret, ref bottomRight))
        {
            return false;
        }

        anchor = ScreenPixelsToDeviceIndependentRect(
            topLeft.X,
            topLeft.Y,
            Math.Max(1, bottomRight.X - topLeft.X),
            Math.Max(1, bottomRight.Y - topLeft.Y));
        return true;
    }

    private static Rect ScreenPixelsToDeviceIndependentRect(double left, double top, double width, double height)
    {
        var source = PresentationSource.CurrentSources.OfType<HwndSource>().FirstOrDefault();
        if (source?.CompositionTarget is null)
        {
            // Without a WPF presentation source, DPI is unknown; use screen pixels as a close first pass.
            return new Rect(left, top, width, height);
        }

        var transform = source.CompositionTarget.TransformFromDevice;
        var topLeft = transform.Transform(new Point(left, top));
        var bottomRight = transform.Transform(new Point(left + width, top + height));
        return new Rect(topLeft, bottomRight);
    }
}
