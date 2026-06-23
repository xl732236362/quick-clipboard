using QuickClipboard.Core.Hotkeys;

namespace QuickClipboard.Core.Services;

public interface IGlobalHotkeyRegistrar
{
    bool Register(string hotkeyId, Hotkey hotkey);

    void Unregister(string hotkeyId);
}
