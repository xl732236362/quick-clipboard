using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Application = System.Windows.Application;

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

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
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

        try
        {
            if (!Clipboard.ContainsText())
            {
                return;
            }

            TextCopied?.Invoke(this, Clipboard.GetText());
        }
        catch (ExternalException)
        {
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
