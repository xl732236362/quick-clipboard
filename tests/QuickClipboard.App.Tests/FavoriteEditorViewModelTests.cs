using FluentAssertions;
using QuickClipboard.App.Presentation.ViewModels;

namespace QuickClipboard.App.Tests;

public sealed class FavoriteEditorViewModelTests
{
    [Fact]
    public void IsValidRequiresTitle()
    {
        var editor = new FavoriteEditorViewModel
        {
            Content = "snippet"
        };

        editor.IsValid.Should().BeFalse();
        editor.ValidationMessage.Should().Be("Title is required.");
    }

    [Fact]
    public void IsValidRequiresContent()
    {
        var editor = new FavoriteEditorViewModel
        {
            Title = "Greeting"
        };

        editor.IsValid.Should().BeFalse();
        editor.ValidationMessage.Should().Be("Content is required.");
    }

    [Fact]
    public void IsValidRejectsInvalidHotkey()
    {
        var editor = new FavoriteEditorViewModel
        {
            Title = "Greeting",
            Content = "hello",
            Hotkey = "Ctrl+Nope"
        };

        editor.IsValid.Should().BeFalse();
        editor.ValidationMessage.Should().Be("Hotkey is invalid.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ctrl+Alt+1")]
    public void IsValidAllowsBlankOrValidHotkey(string? hotkey)
    {
        var editor = new FavoriteEditorViewModel
        {
            Title = "Greeting",
            Content = "hello",
            Hotkey = hotkey
        };

        editor.IsValid.Should().BeTrue();
        editor.ValidationMessage.Should().BeNull();
    }
}
