using System.Windows;
using System.Windows.Input;

namespace QuickClipboard.App.Presentation;

internal static class FloatingPanelActivationPolicy
{
    internal const int WM_MOUSEACTIVATE = 0x0021;
    internal const int MA_NOACTIVATE = 3;

    public static IntPtr HandleMouseActivate(bool shouldAllowActivation, ref bool handled)
    {
        if (shouldAllowActivation)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(MA_NOACTIVATE);
    }

    public static bool ShouldAllowActivation(Window window)
    {
        try
        {
            var hit = window.InputHitTest(Mouse.GetPosition(window)) as DependencyObject;
            return HasEditableAncestor(hit);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool HasEditableAncestor(DependencyObject? element)
    {
        for (var current = element; current is not null; current = GetParent(current))
        {
            if (current is System.Windows.Controls.Primitives.TextBoxBase
                or System.Windows.Controls.PasswordBox
                or System.Windows.Controls.ComboBox)
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        return element is FrameworkElement { Parent: { } parent }
            ? parent
            : System.Windows.Media.VisualTreeHelper.GetParent(element);
    }
}
