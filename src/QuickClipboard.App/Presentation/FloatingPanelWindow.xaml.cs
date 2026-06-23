using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using QuickClipboard.App.Presentation.ViewModels;
using QuickClipboard.Infrastructure.Windows;

namespace QuickClipboard.App.Presentation;

public partial class FloatingPanelWindow : Window, IFloatingPanelWindow
{
    private readonly FloatingPanelViewModel viewModel;
    private readonly PanelPositionService panelPositionService;
    private readonly Rect anchor;
    private bool isClosed;

    public FloatingPanelWindow(
        FloatingPanelViewModel viewModel,
        PanelPositionService panelPositionService,
        Rect anchor)
    {
        this.viewModel = viewModel;
        this.panelPositionService = panelPositionService;
        this.anchor = anchor;

        InitializeComponent();
        DataContext = viewModel;

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        Deactivated += OnDeactivated;
        Closed += OnClosed;
        viewModel.CloseRequested += OnCloseRequested;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionPanel();

        try
        {
            await viewModel.RefreshCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FloatingPanelWindow refresh failed: {ex}");
        }
    }

    private void PositionPanel()
    {
        Measure(new System.Windows.Size(Width, MaxHeight));
        var panelSize = new System.Windows.Size(
            ActualWidth > 0 ? ActualWidth : DesiredSize.Width,
            ActualHeight > 0 ? ActualHeight : DesiredSize.Height);
        var topLeft = panelPositionService.ClampPanelTopLeft(anchor, panelSize);
        Left = topLeft.X;
        Top = topLeft.Y;
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        isClosed = true;
        Loaded -= OnLoaded;
        KeyDown -= OnKeyDown;
        Deactivated -= OnDeactivated;
        Closed -= OnClosed;
        viewModel.CloseRequested -= OnCloseRequested;
    }

    void IFloatingPanelWindow.Close()
    {
        if (!isClosed)
        {
            Close();
        }
    }
}
