using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<PanelPositionService>();
        services.AddSingleton<TextInsertionService>();
        services.AddSingleton<ITextInsertionService>(provider => provider.GetRequiredService<TextInsertionService>());
        services.AddSingleton<TrayApplicationService>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
