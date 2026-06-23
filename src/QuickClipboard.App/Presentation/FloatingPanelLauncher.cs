using System.Windows;
using QuickClipboard.App.Presentation.ViewModels;
using QuickClipboard.Core.Services;

namespace QuickClipboard.App.Presentation;

public interface IPanelAnchorProvider
{
    Rect GetPreferredAnchor();
}

public interface IFloatingPanelWindow
{
    event EventHandler? Closed;

    void Show();

    void Close();
}

public sealed class FloatingPanelLauncher(
    Func<FloatingPanelViewModel> viewModelFactory,
    IPanelAnchorProvider panelAnchorProvider,
    IForegroundWindowRestorer foregroundWindowRestorer,
    Func<FloatingPanelViewModel, Rect, IFloatingPanelWindow> windowFactory)
{
    private IFloatingPanelWindow? currentWindow;

    public void Open()
    {
        Close();

        var viewModel = viewModelFactory();
        var target = foregroundWindowRestorer.CaptureCurrent();
        viewModel.RestorePasteTarget = cancellationToken => foregroundWindowRestorer.RestoreAsync(target, cancellationToken);
        var anchor = panelAnchorProvider.GetPreferredAnchor();
        currentWindow = windowFactory(viewModel, anchor);
        currentWindow.Closed += OnCurrentWindowClosed;
        currentWindow.Show();
    }

    public void Close()
    {
        var window = currentWindow;
        if (window is not null)
        {
            window.Closed -= OnCurrentWindowClosed;
            window.Close();
        }

        currentWindow = null;
    }

    private void OnCurrentWindowClosed(object? sender, EventArgs e)
    {
        var window = currentWindow;
        if (window is not null && ReferenceEquals(sender, window))
        {
            window.Closed -= OnCurrentWindowClosed;
            currentWindow = null;
        }
    }
}
