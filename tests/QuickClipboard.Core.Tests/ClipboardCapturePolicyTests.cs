using FluentAssertions;
using QuickClipboard.Core.Clipboard;
using QuickClipboard.Core.Models;

namespace QuickClipboard.Core.Tests;

public sealed class ClipboardCapturePolicyTests
{
    [Fact]
    public void Evaluate_AcceptsNormalText()
    {
        var policy = CreatePolicy();

        var decision = policy.Evaluate("hello", latestContentHash: null, AppSettings.Defaults);

        decision.Accepted.Should().BeTrue();
        decision.ContentHash.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456")]
    public void Evaluate_RejectsEmptyOrSensitiveText(string value)
    {
        var policy = CreatePolicy();

        var decision = policy.Evaluate(value, latestContentHash: null, AppSettings.Defaults);

        decision.Accepted.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_RejectsImmediateDuplicate()
    {
        var policy = CreatePolicy();
        var first = policy.Evaluate("same", latestContentHash: null, AppSettings.Defaults);

        var second = policy.Evaluate("same", first.ContentHash, AppSettings.Defaults);

        second.Accepted.Should().BeFalse();
        second.Reason.Should().Be("duplicate");
    }

    [Fact]
    public void Evaluate_RejectsTextAboveConfiguredLength()
    {
        var policy = CreatePolicy();
        var settings = AppSettings.Defaults with { MaximumTextLength = 5 };

        var decision = policy.Evaluate("123456", latestContentHash: null, settings);

        decision.Accepted.Should().BeFalse();
        decision.Reason.Should().Be("too_long");
    }

    private static ClipboardCapturePolicy CreatePolicy()
    {
        var normalizer = new TextNormalizer();
        return new ClipboardCapturePolicy(normalizer, new ContentHash(normalizer), new SensitiveTextDetector());
    }
}
