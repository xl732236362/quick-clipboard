using QuickClipboard.Core.Services;

namespace QuickClipboard.Infrastructure.Windows;

public static class TextInsertionServiceFactory
{
    public static TextInsertionService CreateNativeClipboardTextInsertionService(
        IClipboardChangeSuppressor clipboardChangeSuppressor,
        IClipboardOwnerWindow clipboardOwnerWindow) =>
        new(
            clipboardChangeSuppressor,
            new WpfClipboardGateway(),
            new NativeClipboardTextWriter(clipboardOwnerWindow),
            new Win32PasteShortcutSender(),
            new Win32TextKeyboard(),
            TimeSpan.FromMilliseconds(500),
            diagnostics: new NativeTextInsertionDiagnostics());
}
