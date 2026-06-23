namespace QuickClipboard.Core.Models;

public sealed record ClipboardItem(
    Guid Id,
    string Content,
    string ContentHash,
    string ContentType,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    int UseCount,
    string? SourceApp);
