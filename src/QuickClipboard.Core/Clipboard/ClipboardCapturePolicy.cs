using QuickClipboard.Core.Models;
using QuickClipboard.Core.Services;

namespace QuickClipboard.Core.Clipboard;

public sealed class ClipboardCapturePolicy(
    ITextNormalizer normalizer,
    IContentHasher hasher,
    ISensitiveTextDetector sensitiveTextDetector)
{
    public ClipboardCaptureDecision Evaluate(string? text, string? latestContentHash, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ClipboardCaptureDecision.Reject("empty");
        }

        var normalized = normalizer.Normalize(text);
        if (normalized.Length == 0)
        {
            return ClipboardCaptureDecision.Reject("empty");
        }

        if (normalized.Length > settings.MaximumTextLength)
        {
            return ClipboardCaptureDecision.Reject("too_long");
        }

        if (sensitiveTextDetector.IsSensitive(normalized))
        {
            return ClipboardCaptureDecision.Reject("sensitive");
        }

        var hash = hasher.Compute(normalized);
        if (string.Equals(hash, latestContentHash, StringComparison.OrdinalIgnoreCase))
        {
            return ClipboardCaptureDecision.Reject("duplicate");
        }

        return ClipboardCaptureDecision.Accept(normalized, hash);
    }
}
