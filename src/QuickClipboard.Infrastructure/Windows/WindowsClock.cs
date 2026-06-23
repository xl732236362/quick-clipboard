using QuickClipboard.Core.Services;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class WindowsClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
