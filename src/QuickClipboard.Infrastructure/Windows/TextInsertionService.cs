using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Application = System.Windows.Application;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class TextInsertionService
{
    private static readonly TimeSpan ClipboardRestoreDelay = TimeSpan.FromMilliseconds(120);

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

    private static async Task InsertTextOnDispatcherAsync(string text, CancellationToken cancellationToken)
    {
        System.Windows.IDataObject? original;

        try
        {
            original = System.Windows.Clipboard.GetDataObject();
            System.Windows.Clipboard.SetText(text, System.Windows.TextDataFormat.UnicodeText);
        }
        catch (ExternalException ex)
        {
            throw new InvalidOperationException("Clipboard is currently unavailable.", ex);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendPasteShortcut();

            await Task.Delay(ClipboardRestoreDelay, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            TryRestoreClipboard(original);
        }
    }

    private static void TryRestoreClipboard(System.Windows.IDataObject? original)
    {
        if (original is null)
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetDataObject(original, true);
        }
        catch (ExternalException ex)
        {
            Debug.WriteLine($"Failed to restore clipboard after text insertion: {ex}");
        }
    }

    private static void SendPasteShortcut()
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
