namespace QuickClipboard.Core.Models;

public sealed record AppSettings(
    string PanelHotkey,
    int HistoryRetentionCount,
    int MaximumTextLength,
    DateTimeOffset? PauseRecordingUntil,
    bool PauseRecordingIndefinitely)
{
    public static AppSettings Defaults { get; } = new(
        PanelHotkey: "Ctrl+Alt+V",
        HistoryRetentionCount: 200,
        MaximumTextLength: 20_000,
        PauseRecordingUntil: null,
        PauseRecordingIndefinitely: false);
}
