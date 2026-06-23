using CommunityToolkit.Mvvm.ComponentModel;
using QuickClipboard.Core.Hotkeys;

namespace QuickClipboard.App.Presentation.ViewModels;

public sealed partial class FavoriteEditorViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string content = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string? hotkey;

    public bool IsValid => ValidationMessage is null;

    public string? ValidationMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                return "请输入标题。";
            }

            if (string.IsNullOrWhiteSpace(Content))
            {
                return "请输入内容。";
            }

            if (!string.IsNullOrWhiteSpace(Hotkey) && !QuickClipboard.Core.Hotkeys.Hotkey.TryParse(Hotkey, out _))
            {
                return "快捷键格式无效。";
            }

            return null;
        }
    }
}
