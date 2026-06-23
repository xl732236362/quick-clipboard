using System.Runtime.InteropServices;
using FluentAssertions;
using QuickClipboard.Infrastructure.Windows;

namespace QuickClipboard.Infrastructure.Tests;

public sealed class NativeMethodsTests
{
    [Fact]
    public void Input_UsesWin32InputSizeForCurrentPlatform()
    {
        var expectedMinimumSize = IntPtr.Size == 8 ? 40 : 28;

        Marshal.SizeOf<NativeMethods.Input>().Should().BeGreaterThanOrEqualTo(expectedMinimumSize);
    }
}
