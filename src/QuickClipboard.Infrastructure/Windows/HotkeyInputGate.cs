using QuickClipboard.Core.Hotkeys;
using QuickClipboard.Core.Services;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class HotkeyInputGate : IHotkeyInputGate
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(15);

    public async Task WaitForModifiersReleasedAsync(
        HotkeyModifiers modifiers,
        CancellationToken cancellationToken = default)
    {
        if (modifiers == HotkeyModifiers.None)
        {
            return;
        }

        var deadline = DateTimeOffset.UtcNow + Timeout;
        while (DateTimeOffset.UtcNow < deadline && AnyModifierPressed(modifiers))
        {
            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    private static bool AnyModifierPressed(HotkeyModifiers modifiers)
    {
        return (modifiers.HasFlag(HotkeyModifiers.Control) && AnyKeyPressed(
                NativeMethods.VK_CONTROL,
                NativeMethods.VK_LCONTROL,
                NativeMethods.VK_RCONTROL))
            || (modifiers.HasFlag(HotkeyModifiers.Alt) && AnyKeyPressed(
                NativeMethods.VK_MENU,
                NativeMethods.VK_LMENU,
                NativeMethods.VK_RMENU))
            || (modifiers.HasFlag(HotkeyModifiers.Shift) && AnyKeyPressed(
                NativeMethods.VK_SHIFT,
                NativeMethods.VK_LSHIFT,
                NativeMethods.VK_RSHIFT))
            || (modifiers.HasFlag(HotkeyModifiers.Windows) && AnyKeyPressed(
                NativeMethods.VK_LWIN,
                NativeMethods.VK_RWIN));
    }

    private static bool AnyKeyPressed(params int[] virtualKeys)
    {
        foreach (var virtualKey in virtualKeys)
        {
            if ((NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0)
            {
                return true;
            }
        }

        return false;
    }
}
