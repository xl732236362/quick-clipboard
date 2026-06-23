using System.IO;

namespace QuickClipboard.Infrastructure.Persistence;

public static class AppDataPathProvider
{
    public static string GetDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "QuickClipboard", "quick-clipboard.db");
    }
}
