using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class ClipboardMonitor : IDisposable
{
    private HwndSource? _source;
    private DateTimeOffset _suppressedUntil;
    private bool _listenerRegistered;
    private bool _disposed;

    public event EventHandler<string>? TextCopied;

    public void Start()
    {
        ThrowIfDisposed();

        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException(
                "ClipboardMonitor must be started after the WPF application dispatcher is available.");

        if (!dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Start);
            return;
        }

        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("QuickClipboardClipboardMonitor")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        if (!NativeMethods.AddClipboardFormatListener(_source.Handle))
        {
            var error = new Win32Exception(Marshal.GetLastPInvokeError());
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;

            throw new InvalidOperationException(
                $"Failed to register clipboard format listener: {error.Message}",
                error);
        }

        _listenerRegistered = true;
    }

    public void SuppressNextChanges(TimeSpan duration)
    {
        ThrowIfDisposed();

        var dispatcher = _source?.Dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => SuppressNextChanges(duration));
            return;
        }

        _suppressedUntil = DateTimeOffset.Now.Add(duration);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        var dispatcher = _source?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Dispose);
            return;
        }

        if (_source is not null)
        {
            if (_listenerRegistered)
            {
                NativeMethods.RemoveClipboardFormatListener(_source.Handle);
                _listenerRegistered = false;
            }

            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }

        _disposed = true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_CLIPBOARDUPDATE)
        {
            return IntPtr.Zero;
        }

        handled = true;
        OnClipboardUpdated();
        return IntPtr.Zero;
    }

    private void OnClipboardUpdated()
    {
        if (DateTimeOffset.Now < _suppressedUntil)
        {
            return;
        }

        string text;

        try
        {
            if (!Clipboard.ContainsText())
            {
                return;
            }

            text = Clipboard.GetText();
        }
        catch (ExternalException)
        {
            return;
        }

        RaiseTextCopied(text);
    }

    private void RaiseTextCopied(string text)
    {
        var handlers = TextCopied;
        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList())
        {
            try
            {
                ((EventHandler<string>)subscriber)(this, text);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ClipboardMonitor TextCopied subscriber failed: {ex}");
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
