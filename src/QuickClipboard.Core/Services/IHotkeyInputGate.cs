using QuickClipboard.Core.Hotkeys;

namespace QuickClipboard.Core.Services;

public interface IHotkeyInputGate
{
    Task WaitForModifiersReleasedAsync(HotkeyModifiers modifiers, CancellationToken cancellationToken = default);
}
