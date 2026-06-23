using QuickClipboard.Core.Hotkeys;

namespace QuickClipboard.Core.Services;

public interface IHotkeyInputGate
{
    Task<bool> WaitForModifiersReleasedAsync(HotkeyModifiers modifiers, CancellationToken cancellationToken = default);
}
