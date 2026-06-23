using FluentAssertions;
using QuickClipboard.Core.Clipboard;

namespace QuickClipboard.Core.Tests;

public sealed class SensitiveTextDetectorTests
{
    [Theory]
    [InlineData("123456")]
    [InlineData("password=my-secret-value")]
    [InlineData("api_key=abcd1234efgh5678ijkl9012")]
    [InlineData("Authorization: Bearer abcdefghijklmnopqrstuvwxyz012345")]
    [InlineData("4111 1111 1111 1111")]
    public void IsSensitive_ReturnsTrueForHighRiskText(string value)
    {
        var detector = new SensitiveTextDetector();

        detector.IsSensitive(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("Meeting notes for tomorrow")]
    [InlineData("https://example.com/docs")]
    public void IsSensitive_ReturnsFalseForNormalText(string value)
    {
        var detector = new SensitiveTextDetector();

        detector.IsSensitive(value).Should().BeFalse();
    }
}
