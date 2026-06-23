using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using QuickClipboard.App.Presentation;
using QuickClipboard.App.Presentation.ViewModels;
using QuickClipboard.App.Tray;
using QuickClipboard.Core.Clipboard;
using QuickClipboard.Core.Services;
using QuickClipboard.Infrastructure.Persistence;
using QuickClipboard.Infrastructure.Windows;

namespace QuickClipboard.App;

public static class Bootstrapper
{
    public static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        var databasePath = AppDataPathProvider.GetDatabasePath();
        var connectionFactory = new SqliteConnectionFactory(databasePath);

        services.AddSingleton<Func<SqliteConnection>>(_ => connectionFactory.CreateConnection);
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<ITextNormalizer, TextNormalizer>();
        services.AddSingleton<IContentHasher, ContentHash>();
        services.AddSingleton<ISensitiveTextDetector, SensitiveTextDetector>();
        services.AddSingleton<ClipboardCapturePolicy>();
        services.AddSingleton<IClipboardRepository, SqliteClipboardRepository>();
        services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
        services.AddSingleton<IClock, WindowsClock>();
        services.AddSingleton<ClipboardMonitor>();
        services.AddSingleton<GlobalHotkeyService>();
        services.AddSingleton<IGlobalHotkeyRegistrar>(provider => provider.GetRequiredService<GlobalHotkeyService>());
        services.AddSingleton<PanelPositionService>();
        services.AddSingleton<IForegroundWindowRestorer, ForegroundWindowRestorer>();
        services.AddSingleton<IHotkeyInputGate, HotkeyInputGate>();
        services.AddSingleton<TextInsertionService>();
        services.AddSingleton<ITextInsertionService>(provider => provider.GetRequiredService<TextInsertionService>());
        services.AddSingleton<FavoriteHotkeyController>();
        services.AddTransient(provider =>
        {
            var viewModel = new FloatingPanelViewModel(
                provider.GetRequiredService<IClipboardRepository>(),
                provider.GetRequiredService<ISettingsRepository>(),
                provider.GetRequiredService<IClock>(),
                provider.GetRequiredService<ITextInsertionService>());
            viewModel.FavoriteHotkeysChangedAsync = cancellationToken =>
                provider.GetRequiredService<TrayApplicationService>().RefreshFavoriteHotkeysAsync(cancellationToken);
            return viewModel;
        });
        services.AddSingleton<Func<FloatingPanelViewModel>>(provider =>
            () => provider.GetRequiredService<FloatingPanelViewModel>());
        services.AddSingleton<IPanelAnchorProvider, PanelAnchorProvider>();
        services.AddSingleton<FloatingPanelLauncher>();
        services.AddSingleton<Func<FloatingPanelViewModel, System.Windows.Rect, IFloatingPanelWindow>>(provider =>
        {
            var panelPositionService = provider.GetRequiredService<PanelPositionService>();
            return (viewModel, anchor) => new FloatingPanelWindow(viewModel, panelPositionService, anchor);
        });
        services.AddSingleton<TrayApplicationService>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
