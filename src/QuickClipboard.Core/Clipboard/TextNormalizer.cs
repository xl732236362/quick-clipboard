using QuickClipboard.Core.Services;

namespace QuickClipboard.Core.Clipboard;

public sealed class TextNormalizer : ITextNormalizer
{
    public string Normalize(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }
}
