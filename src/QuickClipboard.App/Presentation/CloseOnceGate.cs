namespace QuickClipboard.App.Presentation;

public sealed class CloseOnceGate
{
    private int closeStarted;

    public bool TryBeginClose()
    {
        return Interlocked.Exchange(ref closeStarted, 1) == 0;
    }
}
