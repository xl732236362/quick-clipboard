namespace QuickClipboard.Core.Clipboard;

public sealed record ClipboardCaptureDecision(
    bool Accepted,
    string? NormalizedContent,
    string? ContentHash,
    string? Reason)
{
    public static ClipboardCaptureDecision Accept(string content, string hash)
    {
        return new ClipboardCaptureDecision(true, content, hash, null);
    }

    public static ClipboardCaptureDecision Reject(string reason)
    {
        return new ClipboardCaptureDecision(false, null, null, reason);
    }
}
