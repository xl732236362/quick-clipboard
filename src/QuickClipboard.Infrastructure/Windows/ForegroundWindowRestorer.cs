using QuickClipboard.Core.Services;
using QuickClipboard.Infrastructure.Diagnostics;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class ForegroundWindowRestorer : IForegroundWindowRestorer
{
    private const int SW_RESTORE = 9;
    private static readonly TimeSpan DefaultPollDelay = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(300);

    private readonly IForegroundWindowGateway windows;
    private readonly TimeSpan pollDelay;
    private readonly TimeSpan timeout;

    public ForegroundWindowRestorer()
        : this(new NativeForegroundWindowGateway(), DefaultPollDelay, DefaultTimeout)
    {
    }

    internal ForegroundWindowRestorer(
        IForegroundWindowGateway windows,
        TimeSpan pollDelay,
        TimeSpan timeout)
    {
        this.windows = windows;
        this.pollDelay = pollDelay;
        this.timeout = timeout;
    }

    public IntPtr CaptureCurrent()
    {
        var foregroundWindow = windows.GetForegroundWindow();
        QuickClipboardDiagnostics.Write($"capture foreground=0x{foregroundWindow.ToInt64():X}");
        return foregroundWindow;
    }

    public async Task RestoreAsync(IntPtr windowHandle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (windowHandle == IntPtr.Zero || !windows.IsWindow(windowHandle))
        {
            return;
        }

        QuickClipboardDiagnostics.Write($"restore requested target=0x{windowHandle.ToInt64():X}");
        if (windows.IsIconic(windowHandle))
        {
            QuickClipboardDiagnostics.Write($"restore target minimized target=0x{windowHandle.ToInt64():X}");
            _ = windows.ShowWindow(windowHandle, SW_RESTORE);
        }

        var setForegroundResult = windows.SetForegroundWindow(windowHandle);
        QuickClipboardDiagnostics.Write(
            $"set foreground target=0x{windowHandle.ToInt64():X} result={setForegroundResult}");
        await WaitUntilForegroundAsync(windowHandle, cancellationToken);
        QuickClipboardDiagnostics.Write(
            $"restore completed target=0x{windowHandle.ToInt64():X} foreground=0x{windows.GetForegroundWindow().ToInt64():X}");
    }

    private async Task WaitUntilForegroundAsync(IntPtr windowHandle, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (windows.GetForegroundWindow() == windowHandle || DateTimeOffset.UtcNow >= deadline)
            {
                return;
            }

            await Task.Delay(pollDelay, cancellationToken);
        }
    }
}

internal interface IForegroundWindowGateway
{
    IntPtr GetForegroundWindow();

    bool IsWindow(IntPtr windowHandle);

    bool IsIconic(IntPtr windowHandle);

    bool ShowWindow(IntPtr windowHandle, int showCommand);

    bool SetForegroundWindow(IntPtr windowHandle);
}

internal sealed class NativeForegroundWindowGateway : IForegroundWindowGateway
{
    public IntPtr GetForegroundWindow()
    {
        return NativeMethods.GetForegroundWindow();
    }

    public bool IsWindow(IntPtr windowHandle)
    {
        return NativeMethods.IsWindow(windowHandle);
    }

    public bool IsIconic(IntPtr windowHandle)
    {
        return NativeMethods.IsIconic(windowHandle);
    }

    public bool ShowWindow(IntPtr windowHandle, int showCommand)
    {
        return NativeMethods.ShowWindow(windowHandle, showCommand);
    }

    public bool SetForegroundWindow(IntPtr windowHandle)
    {
        return NativeMethods.SetForegroundWindow(windowHandle);
    }
}
