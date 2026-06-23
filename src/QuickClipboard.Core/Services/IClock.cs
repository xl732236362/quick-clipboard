namespace QuickClipboard.Core.Services;

public interface IClock
{
    DateTimeOffset Now { get; }
}
