namespace QuickClipboard.Core.Services;

public interface ITextInsertionService
{
    Task InsertTextAsync(string text, CancellationToken cancellationToken = default);
}
