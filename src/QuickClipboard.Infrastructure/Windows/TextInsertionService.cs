using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using QuickClipboard.Core.Services;
using Application = System.Windows.Application;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class TextInsertionService : ITextInsertionService
{
    private static readonly TimeSpan ClipboardRestoreDelay = TimeSpan.FromMilliseconds(120);

    private readonly IClipboardChangeSuppressor clipboardChangeSuppressor;
    private readonly IClipboardGateway clipboardGateway;
    private readonly IPasteShortcutSender pasteShortcutSender;
    private readonly TimeSpan clipboardRestoreDelay;

    public TextInsertionService(IClipboardChangeSuppressor clipboardChangeSuppressor)
        : this(
            clipboardChangeSuppressor,
            new WpfClipboardGateway(),
            new Win32PasteShortcutSender(),
            ClipboardRestoreDelay)
    {
    }

    internal TextInsertionService(
        IClipboardChangeSuppressor clipboardChangeSuppressor,
        IClipboardGateway clipboardGateway,
        IPasteShortcutSender pasteShortcutSender,
        TimeSpan clipboardRestoreDelay)
    {
        this.clipboardChangeSuppressor = clipboardChangeSuppressor;
        this.clipboardGateway = clipboardGateway;
        this.pasteShortcutSender = pasteShortcutSender;
        this.clipboardRestoreDelay = clipboardRestoreDelay;
    }

    public Task InsertTextAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            return dispatcher.InvokeAsync(() => InsertTextOnDispatcherAsync(text, cancellationToken)).Task.Unwrap();
        }

        return InsertTextOnDispatcherAsync(text, cancellationToken);
    }

    private async Task InsertTextOnDispatcherAsync(string text, CancellationToken cancellationToken)
    {
        System.Windows.IDataObject? original;

        try
        {
            clipboardChangeSuppressor.SuppressNextChanges();
            original = clipboardGateway.GetDataObject();
            clipboardGateway.SetText(text);
        }
        catch (ExternalException ex)
        {
            throw new InvalidOperationException("Clipboard is currently unavailable.", ex);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            pasteShortcutSender.SendPasteShortcut();

            await Task.Delay(clipboardRestoreDelay, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            TryRestoreClipboard(original);
        }
    }

    private void TryRestoreClipboard(System.Windows.IDataObject? original)
    {
        if (original is null)
        {
            return;
        }

        try
        {
            clipboardGateway.SetDataObject(original, true);
        }
        catch (ExternalException ex)
        {
            Debug.WriteLine($"Failed to restore clipboard after text insertion: {ex}");
        }
    }
}

internal interface IClipboardGateway
{
    System.Windows.IDataObject? GetDataObject();

    void SetText(string text);

    void SetDataObject(System.Windows.IDataObject dataObject, bool copy);
}

internal interface IPasteShortcutSender
{
    void SendPasteShortcut();
}

internal sealed class WpfClipboardGateway : IClipboardGateway
{
    public System.Windows.IDataObject? GetDataObject()
    {
        return System.Windows.Clipboard.GetDataObject();
    }

    public void SetText(string text)
    {
        System.Windows.Clipboard.SetText(text, System.Windows.TextDataFormat.UnicodeText);
    }

    public void SetDataObject(System.Windows.IDataObject dataObject, bool copy)
    {
        System.Windows.Clipboard.SetDataObject(dataObject, copy);
    }
}

internal sealed class Win32PasteShortcutSender : IPasteShortcutSender
{
    public void SendPasteShortcut()
    {
        var inputs = new[]
        {
            CreateKeyboardInput(NativeMethods.VK_CONTROL, 0),
            CreateKeyboardInput(NativeMethods.VK_V, 0),
            CreateKeyboardInput(NativeMethods.VK_V, NativeMethods.KEYEVENTF_KEYUP),
            CreateKeyboardInput(NativeMethods.VK_CONTROL, NativeMethods.KEYEVENTF_KEYUP)
        };

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.Input>());
        if (sent != inputs.Length)
        {
            var error = new Win32Exception(Marshal.GetLastPInvokeError());
            TryReleasePasteShortcutKeys();
            throw new InvalidOperationException(
                $"Failed to send paste shortcut. Sent {sent} of {inputs.Length} keyboard events: {error.Message}",
                error);
        }
    }

    private static void TryReleasePasteShortcutKeys()
    {
        var cleanupInputs = new[]
        {
            CreateKeyboardInput(NativeMethods.VK_V, NativeMethods.KEYEVENTF_KEYUP),
            CreateKeyboardInput(NativeMethods.VK_CONTROL, NativeMethods.KEYEVENTF_KEYUP)
        };

        var sent = NativeMethods.SendInput(
            (uint)cleanupInputs.Length,
            cleanupInputs,
            Marshal.SizeOf<NativeMethods.Input>());

        if (sent != cleanupInputs.Length)
        {
            var error = new Win32Exception(Marshal.GetLastPInvokeError());
            Debug.WriteLine(
                $"Failed to release paste shortcut keys after partial SendInput. Sent {sent} of {cleanupInputs.Length} keyboard events: {error.Message}");
        }
    }

    private static NativeMethods.Input CreateKeyboardInput(ushort virtualKey, uint flags)
    {
        return new NativeMethods.Input
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KeyboardInput
                {
                    wVk = virtualKey,
                    dwFlags = flags
                }
            }
        };
    }
}
