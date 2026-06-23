using System.Runtime.InteropServices;
using System.Text;
using FluentAssertions;
using QuickClipboard.Infrastructure.Windows;

namespace QuickClipboard.Infrastructure.Tests;

public sealed class NativeClipboardTextWriterTests
{
    [Fact]
    public void SetUnicodeTextWritesNullTerminatedUnicodeTextToClipboard()
    {
        using var native = new FakeClipboardNativeApi();
        var writer = new NativeClipboardTextWriter(
            native,
            new FakeClipboardOwnerWindow(new IntPtr(0x1234)));

        writer.SetUnicodeText("hello");

        native.Calls.Should().Equal(
            "open:4660",
            "empty",
            "alloc:12",
            "lock",
            "unlock",
            "set:13",
            "close");
        native.ClipboardText.Should().Be("hello");
    }

    [Fact]
    public void SetUnicodeTextFreesAllocatedMemoryAndClosesClipboardWhenSetClipboardDataFails()
    {
        using var native = new FakeClipboardNativeApi
        {
            ShouldFailSetClipboardData = true
        };
        var writer = new NativeClipboardTextWriter(
            native,
            new FakeClipboardOwnerWindow(new IntPtr(0x1234)));

        var act = () => writer.SetUnicodeText("hello");

        act.Should().Throw<ExternalException>();
        native.Calls.Should().ContainInOrder(
            "open:4660",
            "empty",
            "alloc:12",
            "lock",
            "unlock",
            "set:13",
            "free",
            "close");
    }

    [Fact]
    public void SetUnicodeTextRequiresClipboardOwnerWindow()
    {
        using var native = new FakeClipboardNativeApi();
        var writer = new NativeClipboardTextWriter(
            native,
            new FakeClipboardOwnerWindow(IntPtr.Zero));

        var act = () => writer.SetUnicodeText("hello");

        act.Should().Throw<ExternalException>();
        native.Calls.Should().BeEmpty();
    }

    private sealed class FakeClipboardOwnerWindow(IntPtr handle) : IClipboardOwnerWindow
    {
        public IntPtr GetHandle()
        {
            return handle;
        }
    }

    private sealed class FakeClipboardNativeApi : IClipboardNativeApi, IDisposable
    {
        private readonly Dictionary<IntPtr, int> allocations = new();
        private int nextHandle = 1;

        public List<string> Calls { get; } = new();
        public string? ClipboardText { get; private set; }
        public bool ShouldFailSetClipboardData { get; init; }

        public bool OpenClipboard(IntPtr ownerWindow)
        {
            Calls.Add($"open:{ownerWindow}");
            return true;
        }

        public bool EmptyClipboard()
        {
            Calls.Add("empty");
            return true;
        }

        public IntPtr SetClipboardData(uint format, IntPtr memoryHandle)
        {
            Calls.Add($"set:{format}");
            ClipboardText = ReadNullTerminatedUnicode(memoryHandle);
            return ShouldFailSetClipboardData ? IntPtr.Zero : memoryHandle;
        }

        public bool CloseClipboard()
        {
            Calls.Add("close");
            return true;
        }

        public IntPtr GlobalAlloc(uint flags, nuint byteCount)
        {
            Calls.Add($"alloc:{byteCount}");
            var memory = Marshal.AllocHGlobal((int)byteCount);
            allocations[memory] = (int)byteCount;
            nextHandle++;
            return memory;
        }

        public IntPtr GlobalLock(IntPtr memoryHandle)
        {
            Calls.Add("lock");
            return memoryHandle;
        }

        public bool GlobalUnlock(IntPtr memoryHandle)
        {
            Calls.Add("unlock");
            return true;
        }

        public IntPtr GlobalFree(IntPtr memoryHandle)
        {
            Calls.Add("free");
            if (allocations.Remove(memoryHandle))
            {
                Marshal.FreeHGlobal(memoryHandle);
            }

            return IntPtr.Zero;
        }

        public int GetLastError()
        {
            return 5;
        }

        public void Dispose()
        {
            foreach (var memory in allocations.Keys.ToArray())
            {
                Marshal.FreeHGlobal(memory);
            }

            allocations.Clear();
        }

        private string ReadNullTerminatedUnicode(IntPtr memoryHandle)
        {
            var byteCount = allocations[memoryHandle];
            var bytes = new byte[byteCount];
            Marshal.Copy(memoryHandle, bytes, 0, bytes.Length);
            var text = Encoding.Unicode.GetString(bytes);
            return text.TrimEnd('\0');
        }
    }
}
