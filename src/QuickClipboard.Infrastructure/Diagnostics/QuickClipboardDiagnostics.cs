using System.Diagnostics;
using System.IO;

namespace QuickClipboard.Infrastructure.Diagnostics;

internal static class QuickClipboardDiagnostics
{
    private static readonly object Gate = new();

    internal static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuickClipboard",
        "diagnostics.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Quick Clipboard diagnostics write failed: {ex}");
        }
    }
}
