using FluentAssertions;
using QuickClipboard.App.Presentation;

namespace QuickClipboard.App.Tests;

public sealed class FloatingPanelActivationPolicyTests
{
    [Fact]
    public void HandleMouseActivatePreventsActivationForPastePanelClicks()
    {
        var handled = false;

        var result = FloatingPanelActivationPolicy.HandleMouseActivate(
            shouldAllowActivation: false,
            ref handled);

        handled.Should().BeTrue();
        result.Should().Be(new IntPtr(FloatingPanelActivationPolicy.MA_NOACTIVATE));
    }

    [Fact]
    public void HandleMouseActivateUsesDefaultActivationForTextEditingClicks()
    {
        var handled = false;

        var result = FloatingPanelActivationPolicy.HandleMouseActivate(
            shouldAllowActivation: true,
            ref handled);

        handled.Should().BeFalse();
        result.Should().Be(IntPtr.Zero);
    }
}
