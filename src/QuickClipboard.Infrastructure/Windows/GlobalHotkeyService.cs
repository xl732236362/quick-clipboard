using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using QuickClipboard.Core.Hotkeys;
using Application = System.Windows.Application;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class GlobalHotkeyService : IDisposable
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWindows = 0x0008;

    private readonly Dictionary<int, string> _hotkeyIdsByNativeId = [];
    private readonly Dictionary<string, int> _nativeIdsByHotkeyId = [];
    private int _nextNativeId;
    private HwndSource? _source;
    private bool _disposed;

    public event EventHandler<string>? HotkeyPressed;

    public bool Register(string hotkeyId, Hotkey hotkey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hotkeyId);
        ArgumentNullException.ThrowIfNull(hotkey);
        ThrowIfDisposed();

        var dispatcher = _source?.Dispatcher ?? Application.Current?.Dispatcher
            ?? throw new InvalidOperationException(
                "GlobalHotkeyService must be used after the WPF application dispatcher is available.");

        if (!dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(() => Register(hotkeyId, hotkey));
        }

        EnsureSource();

        var modifiers = GetNativeModifiers(hotkey.Modifiers);
        if (!TryGetVirtualKey(hotkey.Key, out var virtualKey))
        {
            return false;
        }

        var newNativeId = GetNextNativeId();
        if (!NativeMethods.RegisterHotKey(_source!.Handle, newNativeId, modifiers, virtualKey))
        {
            var error = new Win32Exception(Marshal.GetLastPInvokeError());
            Debug.WriteLine($"GlobalHotkeyService failed to register '{hotkeyId}' ({hotkey}): {error.Message}");
            return false;
        }

        if (_nativeIdsByHotkeyId.TryGetValue(hotkeyId, out var oldNativeId))
        {
            UnregisterNativeId(hotkeyId, oldNativeId);
        }

        _nativeIdsByHotkeyId[hotkeyId] = newNativeId;
        _hotkeyIdsByNativeId[newNativeId] = hotkeyId;
        return true;
    }

    public void Unregister(string hotkeyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hotkeyId);
        ThrowIfDisposed();

        var dispatcher = _source?.Dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => Unregister(hotkeyId));
            return;
        }

        UnregisterCore(hotkeyId);
    }

    public void UnregisterAll()
    {
        ThrowIfDisposed();

        var dispatcher = _source?.Dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(UnregisterAll);
            return;
        }

        UnregisterAllCore();
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

        UnregisterAllCore();

        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }

        _disposed = true;
    }

    private void EnsureSource()
    {
        if (_source is not null)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException(
                "GlobalHotkeyService must be used after the WPF application dispatcher is available.");

        if (!dispatcher.CheckAccess())
        {
            dispatcher.Invoke(EnsureSource);
            return;
        }

        var parameters = new HwndSourceParameters("QuickClipboardGlobalHotkeyService")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_HOTKEY)
        {
            return IntPtr.Zero;
        }

        handled = true;

        if (_hotkeyIdsByNativeId.TryGetValue(wParam.ToInt32(), out var hotkeyId))
        {
            RaiseHotkeyPressed(hotkeyId);
        }

        return IntPtr.Zero;
    }

    private void RaiseHotkeyPressed(string hotkeyId)
    {
        var handlers = HotkeyPressed;
        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList())
        {
            try
            {
                ((EventHandler<string>)subscriber)(this, hotkeyId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GlobalHotkeyService HotkeyPressed subscriber failed: {ex}");
            }
        }
    }

    private void UnregisterCore(string hotkeyId)
    {
        if (!_nativeIdsByHotkeyId.Remove(hotkeyId, out var nativeId))
        {
            return;
        }

        UnregisterNativeId(hotkeyId, nativeId);
    }

    private void UnregisterNativeId(string hotkeyId, int nativeId)
    {
        _hotkeyIdsByNativeId.Remove(nativeId);

        if (_source is not null && !NativeMethods.UnregisterHotKey(_source.Handle, nativeId))
        {
            var error = new Win32Exception(Marshal.GetLastPInvokeError());
            Debug.WriteLine($"GlobalHotkeyService failed to unregister '{hotkeyId}': {error.Message}");
        }
    }

    private void UnregisterAllCore()
    {
        foreach (var hotkeyId in _nativeIdsByHotkeyId.Keys.ToArray())
        {
            UnregisterCore(hotkeyId);
        }
    }

    private int GetNextNativeId()
    {
        do
        {
            _nextNativeId++;
            if (_nextNativeId == 0)
            {
                _nextNativeId++;
            }
        }
        while (_hotkeyIdsByNativeId.ContainsKey(_nextNativeId));

        return _nextNativeId;
    }

    private static uint GetNativeModifiers(HotkeyModifiers modifiers)
    {
        var nativeModifiers = 0u;

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            nativeModifiers |= ModControl;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            nativeModifiers |= ModAlt;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            nativeModifiers |= ModShift;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            nativeModifiers |= ModWindows;
        }

        return nativeModifiers;
    }

    private static bool TryGetVirtualKey(string key, out uint virtualKey)
    {
        virtualKey = 0;
        var wpfKeyName = string.Equals(key, "Escape", StringComparison.OrdinalIgnoreCase)
            ? "Esc"
            : key;

        try
        {
            var wpfKey = (Key)new KeyConverter().ConvertFromString(wpfKeyName)!;
            virtualKey = (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
            return virtualKey != 0;
        }
        catch (NotSupportedException ex)
        {
            Debug.WriteLine($"GlobalHotkeyService could not convert key '{key}': {ex}");
            return false;
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"GlobalHotkeyService could not convert key '{key}': {ex}");
            return false;
        }
        catch (FormatException ex)
        {
            Debug.WriteLine($"GlobalHotkeyService could not convert key '{key}': {ex}");
            return false;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
