namespace QuickClipboard.Core.Services;

public interface IContentHasher
{
    string Compute(string value);
}
