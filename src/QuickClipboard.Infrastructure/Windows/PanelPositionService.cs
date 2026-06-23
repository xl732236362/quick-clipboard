using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class PanelPositionService
{
    private const double FallbackAnchorSize = 8;
    private const double PanelGap = 8;
    private const double DefaultDpi = 96;
    private const int TextPattern2Id = 10024;

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
        return ScreenPixelsToDeviceIndependentRect(
            mousePosition.X,
            mousePosition.Y,
            FallbackAnchorSize,
            FallbackAnchorSize);
    }

    public Point ClampPanelTopLeft(Rect anchor, Size panelSize)
    {
        var workingArea = GetWorkingAreaForAnchor(anchor);
        return CalculateAdaptivePanelTopLeft(anchor, panelSize, workingArea);
    }

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

            var textPattern2 = AutomationPattern.LookupById(TextPattern2Id);
            if (textPattern2 is null || !focusedElement.TryGetCurrentPattern(textPattern2, out var pattern))
            {
                return false;
            }

            var range = GetCaretRange(pattern);
            if (range is null)
            {
                return false;
            }

            foreach (var rectangle in range.GetBoundingRectangles())
            {
                if (rectangle.Width > 0 && rectangle.Height > 0)
                {
                    anchor = ScreenPixelsToDeviceIndependentRect(
                        rectangle.Left,
                        rectangle.Top,
                        rectangle.Width,
                        rectangle.Height);
                    return true;
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
        catch (TargetInvocationException ex) when (ex.InnerException is COMException or InvalidOperationException)
        {
            Debug.WriteLine($"PanelPositionService UI Automation caret lookup failed: {ex.InnerException}");
        }

        return false;
    }

    private static TextPatternRange? GetCaretRange(object textPattern2)
    {
        var method = textPattern2.GetType().GetMethod(
            "GetCaretRange",
            BindingFlags.Instance | BindingFlags.Public);

        if (method is null)
        {
            return null;
        }

        object?[] parameters = [false];
        return method.Invoke(textPattern2, parameters) as TextPatternRange;
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

    private static double ClampToWorkingAxis(double value, double panelLength, double workingStart, double workingEnd)
    {
        var maxStart = workingEnd - panelLength;
        if (maxStart < workingStart)
        {
            return workingStart;
        }

        return Math.Clamp(value, workingStart, maxStart);
    }

    private static Rect ScreenPixelsToDeviceIndependentRect(double left, double top, double width, double height)
    {
        var transform = GetTransformFromDevice();
        var topLeft = transform.Transform(new Point(left, top));
        var bottomRight = transform.Transform(new Point(left + width, top + height));
        return new Rect(topLeft, bottomRight);
    }

    private static Point DeviceIndependentPointToScreenPixels(Point point)
    {
        return GetTransformToDevice().Transform(point);
    }

    private static System.Windows.Media.Matrix GetTransformFromDevice()
    {
        var source = PresentationSource.CurrentSources.OfType<HwndSource>().FirstOrDefault();
        if (source?.CompositionTarget is not null)
        {
            return source.CompositionTarget.TransformFromDevice;
        }

        // Without a WPF presentation source, per-monitor DPI is unknown; system DPI is a best-effort fallback.
        var scale = GetSystemDpiScale();
        var matrix = System.Windows.Media.Matrix.Identity;
        matrix.Scale(1 / scale, 1 / scale);
        return matrix;
    }

    private static System.Windows.Media.Matrix GetTransformToDevice()
    {
        var source = PresentationSource.CurrentSources.OfType<HwndSource>().FirstOrDefault();
        if (source?.CompositionTarget is not null)
        {
            return source.CompositionTarget.TransformToDevice;
        }

        // Without a WPF presentation source, per-monitor DPI is unknown; system DPI is a best-effort fallback.
        var scale = GetSystemDpiScale();
        var matrix = System.Windows.Media.Matrix.Identity;
        matrix.Scale(scale, scale);
        return matrix;
    }

    private static double GetSystemDpiScale()
    {
        var dpi = NativeMethods.GetDpiForSystem();
        return dpi > 0 ? dpi / DefaultDpi : 1;
    }
}
