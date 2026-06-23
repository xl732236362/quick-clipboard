namespace QuickClipboard.Core.Services;

public interface IForegroundWindowRestorer
{
    IntPtr CaptureCurrent();

    Task RestoreAsync(IntPtr windowHandle, CancellationToken cancellationToken = default);
}
