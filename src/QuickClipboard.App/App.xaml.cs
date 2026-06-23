using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using QuickClipboard.App.Tray;

namespace QuickClipboard.App;

public partial class App
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        _serviceProvider = Bootstrapper.BuildServices();

        var trayService = _serviceProvider.GetRequiredService<TrayApplicationService>();
        _ = StartTrayApplicationAsync(trayService);
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static async Task StartTrayApplicationAsync(TrayApplicationService trayService)
    {
        try
        {
            await trayService.StartAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Quick Clipboard startup failed: {ex}");
            System.Windows.MessageBox.Show(
                "Quick Clipboard 启动失败。请查看调试输出了解详情。",
                "Quick Clipboard",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Current.Shutdown();
        }
    }
}
