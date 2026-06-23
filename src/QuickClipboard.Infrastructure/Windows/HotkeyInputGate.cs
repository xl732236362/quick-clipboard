using QuickClipboard.Core.Hotkeys;
using QuickClipboard.Core.Services;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class HotkeyInputGate : IHotkeyInputGate
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(15);

    private readonly Func<HotkeyModifiers, bool> anyModifierPressed;
    private readonly TimeSpan timeout;
    private readonly TimeSpan pollInterval;

    public HotkeyInputGate()
        : this(AnyModifierPressed, DefaultTimeout, DefaultPollInterval)
    {
    }

    internal HotkeyInputGate(
        Func<HotkeyModifiers, bool> anyModifierPressed,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        this.anyModifierPressed = anyModifierPressed;
        this.timeout = timeout;
        this.pollInterval = pollInterval;
    }

    public async Task<bool> WaitForModifiersReleasedAsync(
        HotkeyModifiers modifiers,
        CancellationToken cancellationToken = default)
    {
        if (modifiers == HotkeyModifiers.None)
        {
            return true;
        }

        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline && anyModifierPressed(modifiers))
        {
            await Task.Delay(pollInterval, cancellationToken);
        }

        return !anyModifierPressed(modifiers);
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
