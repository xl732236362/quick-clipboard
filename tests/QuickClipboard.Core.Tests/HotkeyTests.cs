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
    [InlineData("Ctrl+Alt+NotAKey")]
    [InlineData("Ctrl+Alt+F25")]
    public void TryParse_RejectsInvalidInput(string value)
    {
        Hotkey.TryParse(value, out var hotkey).Should().BeFalse();
        hotkey.Should().BeNull();
    }

    [Fact]
    public void TryParse_NormalizesWhitespaceAndNamedModifiers()
    {
        var success = Hotkey.TryParse(" control + windows + f12 ", out var hotkey);

        success.Should().BeTrue();
        hotkey.Should().NotBeNull();
        hotkey!.Modifiers.Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Windows);
        hotkey.Key.Should().Be("F12");
        hotkey.ToString().Should().Be("Ctrl+Win+F12");
    }

    [Fact]
    public void TryParse_NormalizesPaddedFunctionKey()
    {
        var success = Hotkey.TryParse("Ctrl+Alt+F01", out var hotkey);

        success.Should().BeTrue();
        hotkey.Should().NotBeNull();
        hotkey!.Modifiers.Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Alt);
        hotkey.Key.Should().Be("F1");
        hotkey.ToString().Should().Be("Ctrl+Alt+F1");
    }

    [Fact]
    public void TryParse_AcceptsSingleDigitKey()
    {
        var success = Hotkey.TryParse("Ctrl+Alt+1", out var hotkey);

        success.Should().BeTrue();
        hotkey.Should().NotBeNull();
        hotkey!.Modifiers.Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Alt);
        hotkey.Key.Should().Be("1");
    }

    [Fact]
    public void ToString_UsesStableSerialization()
    {
        var hotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift, "D");

        hotkey.ToString().Should().Be("Ctrl+Shift+D");
    }
}
