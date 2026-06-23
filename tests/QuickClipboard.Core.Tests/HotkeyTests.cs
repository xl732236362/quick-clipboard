using FluentAssertions;
using QuickClipboard.Core.Hotkeys;

namespace QuickClipboard.Core.Tests;

public sealed class HotkeyTests
{
    [Fact]
    public void TryParse_ParsesDefaultPanelHotkey()
    {
        var success = Hotkey.TryParse("Ctrl+Alt+V", out var hotkey);

        success.Should().BeTrue();
        hotkey.Should().NotBeNull();
        hotkey!.Modifiers.Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Alt);
        hotkey.Key.Should().Be("V");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl")]
    [InlineData("V")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Alt+")]
    [InlineData("Ctrl+Foo+V")]
    public void TryParse_RejectsInvalidInput(string value)
    {
        Hotkey.TryParse(value, out var hotkey).Should().BeFalse();
        hotkey.Should().BeNull();
    }

    [Fact]
    public void ToString_UsesStableSerialization()
    {
        var hotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift, "D");

        hotkey.ToString().Should().Be("Ctrl+Shift+D");
    }
}
