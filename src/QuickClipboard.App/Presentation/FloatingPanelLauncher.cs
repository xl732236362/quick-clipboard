using System.Windows;
using QuickClipboard.App.Presentation.ViewModels;

namespace QuickClipboard.App.Presentation;

public interface IPanelAnchorProvider
{
    Rect GetPreferredAnchor();
}

public interface IFloatingPanelWindow
{
    void Show();

    void Close();
}

public sealed class FloatingPanelLauncher(
    Func<FloatingPanelViewModel> viewModelFactory,
    IPanelAnchorProvider panelAnchorProvider,
    Func<FloatingPanelViewModel, Rect, IFloatingPanelWindow> windowFactory)
{
    private IFloatingPanelWindow? currentWindow;

    public void Open()
    {
        currentWindow?.Close();

        var viewModel = viewModelFactory();
        var anchor = panelAnchorProvider.GetPreferredAnchor();
        currentWindow = windowFactory(viewModel, anchor);
        currentWindow.Show();
    }

    public void Close()
    {
        currentWindow?.Close();
        currentWindow = null;
    }
}
