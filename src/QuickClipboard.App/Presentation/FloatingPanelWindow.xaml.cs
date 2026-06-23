using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using QuickClipboard.App.Presentation.ViewModels;
using QuickClipboard.Infrastructure.Windows;

namespace QuickClipboard.App.Presentation;

public partial class FloatingPanelWindow : Window, IFloatingPanelWindow
{
    private readonly FloatingPanelViewModel viewModel;
    private readonly PanelPositionService panelPositionService;
    private readonly Rect anchor;
    private readonly CloseOnceGate closeOnceGate = new();
    private HwndSource? hwndSource;
    private bool isClosed;
    private bool isPositioning;

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
        SourceInitialized += OnSourceInitialized;
        SizeChanged += OnSizeChanged;
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

        PositionPanel();
    }

    private void PositionPanel()
    {
        if (isPositioning)
        {
            return;
        }

        isPositioning = true;
        try
        {
            Measure(new System.Windows.Size(Width, MaxHeight));
            var panelSize = new System.Windows.Size(
                ActualWidth > 0 ? ActualWidth : DesiredSize.Width,
                ActualHeight > 0 ? ActualHeight : DesiredSize.Height);
            if (panelSize.Width <= 0 || panelSize.Height <= 0)
            {
                return;
            }

            var topLeft = panelPositionService.ClampPanelTopLeft(anchor, panelSize);
            if (!AreClose(Left, topLeft.X))
            {
                Left = topLeft.X;
            }

            if (!AreClose(Top, topLeft.Y))
            {
                Top = topLeft.Y;
            }
        }
        finally
        {
            isPositioning = false;
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        PositionPanel();
    }

    private static bool AreClose(double left, double right)
    {
        return !double.IsNaN(left)
            && !double.IsNaN(right)
            && Math.Abs(left - right) < 0.5;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
        hwndSource?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != FloatingPanelActivationPolicy.WM_MOUSEACTIVATE)
        {
            return IntPtr.Zero;
        }

        return FloatingPanelActivationPolicy.HandleMouseActivate(
            FloatingPanelActivationPolicy.ShouldAllowActivation(this),
            ref handled);
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        RequestClose();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        RequestClose();
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        RequestClose();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        isClosed = true;
        Loaded -= OnLoaded;
        SourceInitialized -= OnSourceInitialized;
        SizeChanged -= OnSizeChanged;
        KeyDown -= OnKeyDown;
        Deactivated -= OnDeactivated;
        Closed -= OnClosed;
        viewModel.CloseRequested -= OnCloseRequested;
        hwndSource?.RemoveHook(WndProc);
        hwndSource = null;
    }

    void IFloatingPanelWindow.Close()
    {
        RequestClose();
    }

    private void RequestClose()
    {
        if (!isClosed && closeOnceGate.TryBeginClose())
        {
            Close();
        }
    }
}
