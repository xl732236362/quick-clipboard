using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace QuickClipboard.Infrastructure.Windows;

internal interface IClipboardTextWriter
{
    void SetUnicodeText(string text);
}

public interface IClipboardOwnerWindow
{
    IntPtr GetHandle();
}

internal interface IClipboardNativeApi
{
    bool OpenClipboard(IntPtr ownerWindow);

    bool EmptyClipboard();

    IntPtr SetClipboardData(uint format, IntPtr memoryHandle);

    bool CloseClipboard();

    IntPtr GlobalAlloc(uint flags, nuint byteCount);

    IntPtr GlobalLock(IntPtr memoryHandle);

    bool GlobalUnlock(IntPtr memoryHandle);

    IntPtr GlobalFree(IntPtr memoryHandle);

    int GetLastError();
}

internal sealed class NativeClipboardTextWriter(
    IClipboardNativeApi native,
    IClipboardOwnerWindow ownerWindow) : IClipboardTextWriter
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint GMEM_ZEROINIT = 0x0040;

    public NativeClipboardTextWriter(IClipboardOwnerWindow ownerWindow)
        : this(new User32ClipboardNativeApi(), ownerWindow)
    {
    }

    public void SetUnicodeText(string text)
    {
        var ownerHandle = ownerWindow.GetHandle();
        if (ownerHandle == IntPtr.Zero)
        {
            throw CreateExternalException("Clipboard owner window is not available.", 0);
        }

        if (!native.OpenClipboard(ownerHandle))
        {
            throw CreateExternalException("OpenClipboard failed.", native.GetLastError());
        }

        var memoryHandle = IntPtr.Zero;
        var clipboardOwnsMemory = false;

        try
        {
            if (!native.EmptyClipboard())
            {
                throw CreateExternalException("EmptyClipboard failed.", native.GetLastError());
            }

            memoryHandle = AllocateUnicodeText(text);
            if (native.SetClipboardData(CF_UNICODETEXT, memoryHandle) == IntPtr.Zero)
            {
                throw CreateExternalException("SetClipboardData failed.", native.GetLastError());
            }

            clipboardOwnsMemory = true;
        }
        finally
        {
            if (memoryHandle != IntPtr.Zero && !clipboardOwnsMemory)
            {
                _ = native.GlobalFree(memoryHandle);
            }

            _ = native.CloseClipboard();
        }
    }

    private IntPtr AllocateUnicodeText(string text)
    {
        var bytes = Encoding.Unicode.GetBytes(text + '\0');
        var memoryHandle = native.GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, (nuint)bytes.Length);
        if (memoryHandle == IntPtr.Zero)
        {
            throw CreateExternalException("GlobalAlloc failed.", native.GetLastError());
        }

        var lockedMemory = native.GlobalLock(memoryHandle);
        if (lockedMemory == IntPtr.Zero)
        {
            _ = native.GlobalFree(memoryHandle);
            throw CreateExternalException("GlobalLock failed.", native.GetLastError());
        }

        try
        {
            Marshal.Copy(bytes, 0, lockedMemory, bytes.Length);
        }
        finally
        {
            _ = native.GlobalUnlock(memoryHandle);
        }

        return memoryHandle;
    }

    private static ExternalException CreateExternalException(string message, int error)
    {
        return new ExternalException($"{message} ({new Win32Exception(error).Message})", error);
    }
}

internal sealed class User32ClipboardNativeApi : IClipboardNativeApi
{
    public bool OpenClipboard(IntPtr ownerWindow)
    {
        return NativeMethods.OpenClipboard(ownerWindow);
    }

    public bool EmptyClipboard()
    {
        return NativeMethods.EmptyClipboard();
    }

    public IntPtr SetClipboardData(uint format, IntPtr memoryHandle)
    {
        return NativeMethods.SetClipboardData(format, memoryHandle);
    }

    public bool CloseClipboard()
    {
        return NativeMethods.CloseClipboard();
    }

    public IntPtr GlobalAlloc(uint flags, nuint byteCount)
    {
        return NativeMethods.GlobalAlloc(flags, byteCount);
    }

    public IntPtr GlobalLock(IntPtr memoryHandle)
    {
        return NativeMethods.GlobalLock(memoryHandle);
    }

    public bool GlobalUnlock(IntPtr memoryHandle)
    {
        return NativeMethods.GlobalUnlock(memoryHandle);
    }

    public IntPtr GlobalFree(IntPtr memoryHandle)
    {
        return NativeMethods.GlobalFree(memoryHandle);
    }

    public int GetLastError()
    {
        return Marshal.GetLastPInvokeError();
    }
}
