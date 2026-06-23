using System.Text.RegularExpressions;
using QuickClipboard.Core.Services;

namespace QuickClipboard.Core.Clipboard;

public sealed partial class SensitiveTextDetector : ISensitiveTextDetector
{
    public bool IsSensitive(string value)
    {
        var text = value.Trim();
        return VerificationCodePattern().IsMatch(text)
            || SecretKeyPattern().IsMatch(text)
            || BearerTokenPattern().IsMatch(text)
            || LooksLikeCreditCard(text);
    }

    private static bool LooksLikeCreditCard(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length is < 13 or > 19)
        {
            return false;
        }

        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var number = digits[i] - '0';
            if (alternate)
            {
                number *= 2;
                if (number > 9)
                {
                    number -= 9;
                }
            }

            sum += number;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    [GeneratedRegex(@"^\d{4,8}$", RegexOptions.CultureInvariant)]
    private static partial Regex VerificationCodePattern();

    [GeneratedRegex(@"(?i)(password|passwd|pwd|secret|api[_-]?key|token)\s*[:=]\s*\S{6,}")]
    private static partial Regex SecretKeyPattern();

    [GeneratedRegex(@"(?i)bearer\s+[a-z0-9._\-]{20,}")]
    private static partial Regex BearerTokenPattern();
}
