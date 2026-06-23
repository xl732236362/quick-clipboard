using System.Security.Cryptography;
using System.Text;
using QuickClipboard.Core.Services;

namespace QuickClipboard.Core.Clipboard;

public sealed class ContentHash(ITextNormalizer normalizer) : IContentHasher
{
    public string Compute(string value)
    {
        var normalized = normalizer.Normalize(value);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
