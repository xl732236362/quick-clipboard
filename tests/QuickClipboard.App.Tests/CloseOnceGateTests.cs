using FluentAssertions;
using QuickClipboard.App.Presentation;

namespace QuickClipboard.App.Tests;

public sealed class CloseOnceGateTests
{
    [Fact]
    public void TryBeginCloseAllowsOnlyOneCloseBeforeWindowClosed()
    {
        var gate = new CloseOnceGate();

        gate.TryBeginClose().Should().BeTrue();
        gate.TryBeginClose().Should().BeFalse();
    }
}
