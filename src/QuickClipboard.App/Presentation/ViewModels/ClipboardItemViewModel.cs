using CommunityToolkit.Mvvm.ComponentModel;
using QuickClipboard.Core.Models;

namespace QuickClipboard.App.Presentation.ViewModels;

public sealed partial class ClipboardItemViewModel : ObservableObject
{
    public ClipboardItemViewModel(ClipboardItem item)
    {
        Id = item.Id;
        Preview = PreviewText.Create(item.Content);
        Content = item.Content;
        CreatedAt = item.CreatedAt;
    }

    public Guid Id { get; }
    public string Preview { get; }
    public string Content { get; }
    public DateTimeOffset CreatedAt { get; }
}

internal static class PreviewText
{
    private const int PreviewLimit = 120;

    public static string Create(string content)
    {
        var preview = content.ReplaceLineEndings(" ");
        return preview.Length <= PreviewLimit ? preview : preview[..PreviewLimit];
    }
}
