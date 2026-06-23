namespace QuickClipboard.Core.Hotkeys;

public sealed record Hotkey(HotkeyModifiers Modifiers, string Key)
{
    private static readonly IReadOnlyDictionary<string, string> NamedKeys = new Dictionary<string, string>
    {
        ["SPACE"] = "Space",
        ["ENTER"] = "Enter",
        ["TAB"] = "Tab",
        ["ESCAPE"] = "Escape",
        ["BACKSPACE"] = "Backspace",
        ["DELETE"] = "Delete",
        ["INSERT"] = "Insert",
        ["HOME"] = "Home",
        ["END"] = "End",
        ["PAGEUP"] = "PageUp",
        ["PAGEDOWN"] = "PageDown",
        ["UP"] = "Up",
        ["DOWN"] = "Down",
        ["LEFT"] = "Left",
        ["RIGHT"] = "Right"
    };

    public static bool TryParse(string? value, out Hotkey? hotkey)
    {
        hotkey = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || parts.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var modifier = parts[i].ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => HotkeyModifiers.Control,
                "ALT" => HotkeyModifiers.Alt,
                "SHIFT" => HotkeyModifiers.Shift,
                "WIN" or "WINDOWS" => HotkeyModifiers.Windows,
                _ => HotkeyModifiers.None
            };

            if (modifier == HotkeyModifiers.None)
            {
                return false;
            }

            modifiers |= modifier;
        }

        if (modifiers == HotkeyModifiers.None || !TryNormalizeKey(parts[^1], out var key))
        {
            return false;
        }

        hotkey = new Hotkey(modifiers, key);
        return true;
    }

    private static bool TryNormalizeKey(string value, out string key)
    {
        key = string.Empty;
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length == 1 && (char.IsAsciiLetterUpper(normalized[0]) || char.IsAsciiDigit(normalized[0])))
        {
            key = normalized;
            return true;
        }

        if (normalized.Length is >= 2 and <= 3 && normalized[0] == 'F' && int.TryParse(normalized[1..], out var functionKey) && functionKey is >= 1 and <= 24)
        {
            key = $"F{functionKey}";
            return true;
        }

        return NamedKeys.TryGetValue(normalized, out key!);
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        if (!TryNormalizeKey(Key, out var key))
        {
            key = Key;
        }

        parts.Add(key);
        return string.Join("+", parts);
    }
}
