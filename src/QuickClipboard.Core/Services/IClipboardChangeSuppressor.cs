namespace QuickClipboard.Core.Services;

public interface IClipboardChangeSuppressor
{
    void SuppressNextChanges();
}
