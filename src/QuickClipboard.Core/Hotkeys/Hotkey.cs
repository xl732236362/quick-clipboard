namespace QuickClipboard.Core.Hotkeys;

public sealed record Hotkey(HotkeyModifiers Modifiers, string Key)
{
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

        var key = parts[^1].Trim().ToUpperInvariant();
        var keyIsModifier = key switch
        {
            "CTRL" or "CONTROL" or "ALT" or "SHIFT" or "WIN" or "WINDOWS" => true,
            _ => false
        };

        if (key.Length == 0 || keyIsModifier || modifiers == HotkeyModifiers.None)
        {
            return false;
        }

        hotkey = new Hotkey(modifiers, key);
        return true;
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

        parts.Add(Key.ToUpperInvariant());
        return string.Join("+", parts);
    }
}
