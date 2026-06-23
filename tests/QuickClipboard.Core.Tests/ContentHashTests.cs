using FluentAssertions;
using QuickClipboard.Core.Clipboard;

namespace QuickClipboard.Core.Tests;

public sealed class ContentHashTests
{
    [Fact]
    public void Compute_ReturnsSameHashForSameNormalizedText()
    {
        var normalizer = new TextNormalizer();
        var hasher = new ContentHash(normalizer);

        var first = hasher.Compute(" hello\r\nworld ");
        var second = hasher.Compute("hello\nworld");

        first.Should().Be(second);
    }

    [Fact]
    public void Normalize_TrimsAndNormalizesLineEndings()
    {
        var normalizer = new TextNormalizer();

        normalizer.Normalize("  a\r\nb\r\n  ").Should().Be("a\nb");
    }
}
