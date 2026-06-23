using FluentAssertions;
using QuickClipboard.Core.Hotkeys;
using QuickClipboard.Infrastructure.Windows;

namespace QuickClipboard.Infrastructure.Tests;

public sealed class HotkeyInputGateTests
{
    [Fact]
    public async Task WaitForModifiersReleasedAsyncReturnsTrueWhenNoModifiersNeedWaiting()
    {
        var calls = 0;
        var gate = new HotkeyInputGate(
            _ =>
            {
                calls++;
                return true;
            },
            timeout: TimeSpan.FromMilliseconds(1),
            pollInterval: TimeSpan.FromMilliseconds(1));

        var released = await gate.WaitForModifiersReleasedAsync(HotkeyModifiers.None);

        released.Should().BeTrue();
        calls.Should().Be(0);
    }

    [Fact]
    public async Task WaitForModifiersReleasedAsyncReturnsTrueWhenModifiersReleaseBeforeTimeout()
    {
        var calls = 0;
        var gate = new HotkeyInputGate(
            _ => Interlocked.Increment(ref calls) == 1,
            timeout: TimeSpan.FromMilliseconds(100),
            pollInterval: TimeSpan.FromMilliseconds(1));

        var released = await gate.WaitForModifiersReleasedAsync(HotkeyModifiers.Control);

        released.Should().BeTrue();
        calls.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task WaitForModifiersReleasedAsyncReturnsFalseWhenModifiersRemainPressedUntilTimeout()
    {
        var gate = new HotkeyInputGate(
            _ => true,
            timeout: TimeSpan.FromMilliseconds(1),
            pollInterval: TimeSpan.FromMilliseconds(1));

        var released = await gate.WaitForModifiersReleasedAsync(HotkeyModifiers.Control);

        released.Should().BeFalse();
    }
}
