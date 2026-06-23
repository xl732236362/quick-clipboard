namespace QuickClipboard.Core.Services;

public interface ISensitiveTextDetector
{
    bool IsSensitive(string value);
}
