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
        editor.ValidationMessage.Should().Be("请输入标题。");
    }

    [Fact]
    public void IsValidRequiresContent()
    {
        var editor = new FavoriteEditorViewModel
        {
            Title = "Greeting"
        };

        editor.IsValid.Should().BeFalse();
        editor.ValidationMessage.Should().Be("请输入内容。");
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
        editor.ValidationMessage.Should().Be("快捷键格式无效。");
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
