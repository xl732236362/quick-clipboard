using QuickClipboard.Core.Services;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class ForegroundWindowRestorer : IForegroundWindowRestorer
{
    private const int SW_RESTORE = 9;

    public IntPtr CaptureCurrent()
    {
        return NativeMethods.GetForegroundWindow();
    }

    public Task RestoreAsync(IntPtr windowHandle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (windowHandle != IntPtr.Zero && NativeMethods.IsWindow(windowHandle))
        {
            _ = NativeMethods.ShowWindow(windowHandle, SW_RESTORE);
            _ = NativeMethods.SetForegroundWindow(windowHandle);
        }

        return Task.CompletedTask;
    }
}
