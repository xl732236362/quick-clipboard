using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using QuickClipboard.Core.Services;
using QuickClipboard.Infrastructure.Diagnostics;
using Application = System.Windows.Application;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class TextInsertionService : ITextInsertionService
{
    private static readonly TimeSpan ClipboardRestoreDelay = TimeSpan.FromMilliseconds(500);
    private const int DefaultClipboardRetryCount = 2;
    private static readonly TimeSpan DefaultClipboardRetryDelay = TimeSpan.FromMilliseconds(40);

    private readonly IClipboardChangeSuppressor clipboardChangeSuppressor;
    private readonly IClipboardGateway clipboardGateway;
    private readonly IClipboardTextWriter clipboardTextWriter;
    private readonly IPasteShortcutSender pasteShortcutSender;
    private readonly ITextKeyboard textKeyboard;
    private readonly ITextInsertionDiagnostics diagnostics;
    private readonly TimeSpan clipboardRestoreDelay;
    private readonly int clipboardRetryCount;
    private readonly TimeSpan clipboardRetryDelay;

    public TextInsertionService(IClipboardChangeSuppressor clipboardChangeSuppressor)
        : this(
            clipboardChangeSuppressor,
            new WpfClipboardGateway(),
            new WpfClipboardTextWriter(),
            new Win32PasteShortcutSender(),
            new Win32TextKeyboard(),
            ClipboardRestoreDelay,
            DefaultClipboardRetryCount,
            DefaultClipboardRetryDelay,
            new NativeTextInsertionDiagnostics())
    {
    }

    internal TextInsertionService(
        IClipboardChangeSuppressor clipboardChangeSuppressor,
        IClipboardGateway clipboardGateway,
        IClipboardTextWriter clipboardTextWriter,
        IPasteShortcutSender pasteShortcutSender,
        ITextKeyboard? textKeyboard,
        TimeSpan clipboardRestoreDelay,
        int clipboardRetryCount = DefaultClipboardRetryCount,
        TimeSpan? clipboardRetryDelay = null,
        ITextInsertionDiagnostics? diagnostics = null)
    {
        this.clipboardChangeSuppressor = clipboardChangeSuppressor;
        this.clipboardGateway = clipboardGateway;
        this.clipboardTextWriter = clipboardTextWriter;
        this.pasteShortcutSender = pasteShortcutSender;
        this.textKeyboard = textKeyboard ?? new Win32TextKeyboard();
        this.diagnostics = diagnostics ?? new NativeTextInsertionDiagnostics();
        this.clipboardRestoreDelay = clipboardRestoreDelay;
        this.clipboardRetryCount = Math.Max(1, clipboardRetryCount);
        this.clipboardRetryDelay = clipboardRetryDelay ?? DefaultClipboardRetryDelay;
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
            original = await SetClipboardTextWithRetryAsync(text, cancellationToken).ConfigureAwait(true);
        }
        catch (ExternalException ex)
        {
            QuickClipboardDiagnostics.Write($"clipboard unavailable, falling back to unicode input: {ex.Message}");
            QuickClipboardDiagnostics.Write(diagnostics.DescribeClipboardOwner());
            QuickClipboardDiagnostics.Write($"unicode input fallback before {diagnostics.DescribeCurrentFocus()}");
            textKeyboard.TypeText(text);
            QuickClipboardDiagnostics.Write($"unicode input events sent length={text.Length}");
            QuickClipboardDiagnostics.Write($"unicode input fallback after {diagnostics.DescribeCurrentFocus()}");
            return;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            QuickClipboardDiagnostics.Write($"paste send start length={text.Length}");
            pasteShortcutSender.SendPasteShortcut();
            QuickClipboardDiagnostics.Write($"paste send completed restoreDelayMs={clipboardRestoreDelay.TotalMilliseconds}");

            await Task.Delay(clipboardRestoreDelay, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            QuickClipboardDiagnostics.Write("clipboard restore start");
            TryRestoreClipboard(original);
            QuickClipboardDiagnostics.Write("clipboard restore completed");
        }
    }

    private async Task<System.Windows.IDataObject?> SetClipboardTextWithRetryAsync(
        string text,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                clipboardChangeSuppressor.SuppressNextChanges();
                QuickClipboardDiagnostics.Write($"clipboard get data start attempt={attempt}");
                var original = clipboardGateway.GetDataObject();
                QuickClipboardDiagnostics.Write($"clipboard set text start length={text.Length} attempt={attempt}");
                clipboardTextWriter.SetUnicodeText(text);
                QuickClipboardDiagnostics.Write($"clipboard set text succeeded length={text.Length} attempt={attempt}");
                return original;
            }
            catch (ExternalException ex) when (attempt < clipboardRetryCount)
            {
                QuickClipboardDiagnostics.Write(
                    $"clipboard set text retry attempt={attempt} error={ex.Message} {diagnostics.DescribeClipboardOwner()}");
                await Task.Delay(clipboardRetryDelay, cancellationToken).ConfigureAwait(true);
            }
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

    void SetDataObject(System.Windows.IDataObject dataObject, bool copy);
}

internal interface IPasteShortcutSender
{
    void SendPasteShortcut();
}

internal interface ITextKeyboard
{
    void TypeText(string text);
}

internal interface ITextInsertionDiagnostics
{
    string DescribeClipboardOwner();

    string DescribeCurrentFocus();
}

internal sealed class WpfClipboardGateway : IClipboardGateway
{
    public System.Windows.IDataObject? GetDataObject()
    {
        return System.Windows.Clipboard.GetDataObject();
    }

    public void SetDataObject(System.Windows.IDataObject dataObject, bool copy)
    {
        System.Windows.Clipboard.SetDataObject(dataObject, copy);
    }
}

internal sealed class WpfClipboardTextWriter : IClipboardTextWriter
{
    public void SetUnicodeText(string text)
    {
        System.Windows.Clipboard.SetText(text, System.Windows.TextDataFormat.UnicodeText);
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

internal sealed class Win32TextKeyboard : ITextKeyboard
{
    public void TypeText(string text)
    {
        foreach (var character in text)
        {
            SendUnicodeCharacter(character);
        }
    }

    private static void SendUnicodeCharacter(char character)
    {
        var inputs = new[]
        {
            CreateKeyboardInput(character, NativeMethods.KEYEVENTF_UNICODE),
            CreateKeyboardInput(character, NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP)
        };

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.Input>());
        if (sent != inputs.Length)
        {
            var error = new Win32Exception(Marshal.GetLastPInvokeError());
            throw new InvalidOperationException(
                $"Failed to send unicode text input. Sent {sent} of {inputs.Length} keyboard events: {error.Message}",
                error);
        }
    }

    private static NativeMethods.Input CreateKeyboardInput(char character, uint flags)
    {
        return new NativeMethods.Input
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KeyboardInput
                {
                    wScan = character,
                    dwFlags = flags
                }
            }
        };
    }
}

internal sealed class NativeTextInsertionDiagnostics : ITextInsertionDiagnostics
{
    public string DescribeClipboardOwner()
    {
        var openClipboardWindow = NativeMethods.GetOpenClipboardWindow();
        var clipboardOwner = NativeMethods.GetClipboardOwner();

        return string.Join(
            " ",
            DescribeWindow("clipboardOpenWindow", openClipboardWindow),
            DescribeWindow("clipboardOwner", clipboardOwner));
    }

    public string DescribeCurrentFocus()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        var threadId = NativeMethods.GetWindowThreadProcessId(foreground, out var processId);
        var guiThreadInfo = new NativeMethods.GuiThreadInfo
        {
            cbSize = Marshal.SizeOf<NativeMethods.GuiThreadInfo>()
        };

        if (threadId == 0 || !NativeMethods.GetGUIThreadInfo(threadId, ref guiThreadInfo))
        {
            var error = Marshal.GetLastPInvokeError();
            return
                $"focus foreground=0x{foreground.ToInt64():X} thread={threadId} pid={processId} guiThreadInfo=False error={error}";
        }

        return
            $"focus foreground=0x{foreground.ToInt64():X} thread={threadId} pid={processId} process={GetProcessName(processId)} " +
            $"active=0x{guiThreadInfo.hwndActive.ToInt64():X} focus=0x{guiThreadInfo.hwndFocus.ToInt64():X} " +
            $"caret=0x{guiThreadInfo.hwndCaret.ToInt64():X} " +
            $"caretRect={guiThreadInfo.rcCaret.Left},{guiThreadInfo.rcCaret.Top},{guiThreadInfo.rcCaret.Right},{guiThreadInfo.rcCaret.Bottom}";
    }

    private static string DescribeWindow(string label, IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return $"{label}=0x0";
        }

        var threadId = NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
        return
            $"{label}=0x{windowHandle.ToInt64():X} thread={threadId} pid={processId} process={GetProcessName(processId)}";
    }

    private static string GetProcessName(uint processId)
    {
        if (processId == 0)
        {
            return "unknown";
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception)
        {
            return "unknown";
        }
    }
}
