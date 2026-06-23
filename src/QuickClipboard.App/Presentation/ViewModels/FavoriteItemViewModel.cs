using CommunityToolkit.Mvvm.ComponentModel;
using QuickClipboard.Core.Models;

namespace QuickClipboard.App.Presentation.ViewModels;

public sealed partial class FavoriteItemViewModel : ObservableObject
{
    public FavoriteItemViewModel(FavoriteItem item)
    {
        Id = item.Id;
        Title = item.Title;
        Preview = PreviewText.Create(item.Content);
        Content = item.Content;
        Hotkey = item.Hotkey;
        SortOrder = item.SortOrder;
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Preview { get; }
    public string Content { get; }
    public string? Hotkey { get; }
    public int SortOrder { get; }
}
