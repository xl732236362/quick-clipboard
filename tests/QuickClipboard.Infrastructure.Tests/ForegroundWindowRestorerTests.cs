using FluentAssertions;
using QuickClipboard.Infrastructure.Windows;

namespace QuickClipboard.Infrastructure.Tests;

public sealed class ForegroundWindowRestorerTests
{
    [Fact]
    public async Task RestoreWaitsUntilTargetWindowIsForeground()
    {
        var target = new IntPtr(123);
        var calls = new List<string>();
        var windows = new FakeForegroundWindowGateway(
            target,
            calls,
            foregroundSequence: [IntPtr.Zero, target]);
        var restorer = new ForegroundWindowRestorer(
            windows,
            pollDelay: TimeSpan.Zero,
            timeout: TimeSpan.FromMilliseconds(100));

        await restorer.RestoreAsync(target);

        calls.Should().ContainInOrder(
            "is-window:123",
            "is-iconic:123",
            "set-foreground:123",
            "get-foreground",
            "get-foreground");
        calls.Count(call => call == "get-foreground").Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task RestoreDoesNotRestoreWindowWhenTargetIsNotMinimized()
    {
        var target = new IntPtr(123);
        var calls = new List<string>();
        var windows = new FakeForegroundWindowGateway(target, calls, foregroundSequence: [target])
        {
            IsIconicResult = false
        };
        var restorer = new ForegroundWindowRestorer(
            windows,
            pollDelay: TimeSpan.Zero,
            timeout: TimeSpan.FromMilliseconds(100));

        await restorer.RestoreAsync(target);

        calls.Should().NotContain(call => call.StartsWith("show:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RestoreRestoresWindowWhenTargetIsMinimized()
    {
        var target = new IntPtr(123);
        var calls = new List<string>();
        var windows = new FakeForegroundWindowGateway(target, calls, foregroundSequence: [target])
        {
            IsIconicResult = true
        };
        var restorer = new ForegroundWindowRestorer(
            windows,
            pollDelay: TimeSpan.Zero,
            timeout: TimeSpan.FromMilliseconds(100));

        await restorer.RestoreAsync(target);

        calls.Should().Contain("show:123");
    }

    private sealed class FakeForegroundWindowGateway(
        IntPtr validWindow,
        List<string> calls,
        IReadOnlyList<IntPtr> foregroundSequence) : IForegroundWindowGateway
    {
        private int getForegroundCallCount;

        public bool IsIconicResult { get; init; }

        public IntPtr GetForegroundWindow()
        {
            calls.Add("get-foreground");
            var index = Math.Min(getForegroundCallCount, foregroundSequence.Count - 1);
            getForegroundCallCount++;
            return foregroundSequence[index];
        }

        public bool IsWindow(IntPtr windowHandle)
        {
            calls.Add($"is-window:{windowHandle}");
            return windowHandle == validWindow;
        }

        public bool IsIconic(IntPtr windowHandle)
        {
            calls.Add($"is-iconic:{windowHandle}");
            return IsIconicResult;
        }

        public bool ShowWindow(IntPtr windowHandle, int showCommand)
        {
            calls.Add($"show:{windowHandle}");
            return true;
        }

        public bool SetForegroundWindow(IntPtr windowHandle)
        {
            calls.Add($"set-foreground:{windowHandle}");
            return true;
        }
    }
}
