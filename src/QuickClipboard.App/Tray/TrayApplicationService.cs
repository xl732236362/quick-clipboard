using System.Diagnostics;
using QuickClipboard.App.Presentation;
using QuickClipboard.Core.Clipboard;
using QuickClipboard.Core.Hotkeys;
using QuickClipboard.Core.Models;
using QuickClipboard.Core.Services;
using QuickClipboard.Infrastructure.Persistence;
using QuickClipboard.Infrastructure.Windows;
using Application = System.Windows.Application;
using Forms = System.Windows.Forms;

namespace QuickClipboard.App.Tray;

public sealed class TrayApplicationService(
    DatabaseInitializer databaseInitializer,
    ClipboardMonitor clipboardMonitor,
    GlobalHotkeyService hotkeyService,
    ClipboardCapturePolicy capturePolicy,
    IClipboardRepository clipboardRepository,
    ISettingsRepository settingsRepository,
    IClock clock,
    FavoriteHotkeyController favoriteHotkeyController,
    FloatingPanelLauncher floatingPanelLauncher) : IDisposable
{
    private const string PanelHotkeyId = "panel";

    private Forms.NotifyIcon? _trayIcon;
    private AppSettings _settings = AppSettings.Defaults;
    private readonly SemaphoreSlim _recordingGate = new(1, 1);
    private readonly SemaphoreSlim _settingsGate = new(1, 1);
    private bool _started;
    private bool _disposed;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_started)
        {
            return;
        }

        await databaseInitializer.InitializeAsync(cancellationToken);
        var loadedSettings = await settingsRepository.LoadAsync(cancellationToken);
        _settings = NormalizeSettings(loadedSettings);
        if (_settings != loadedSettings)
        {
            try
            {
                await SaveSettingsAsync(_settings, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save normalized settings. {ex}");
            }
        }

        CreateTrayIcon();
        RegisterPanelHotkey(_settings);
        await RefreshFavoriteHotkeysAsync(cancellationToken);

        clipboardMonitor.TextCopied += OnTextCopied;
        clipboardMonitor.Start();

        _started = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        clipboardMonitor.TextCopied -= OnTextCopied;
        hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        floatingPanelLauncher.Close();

        clipboardMonitor.Dispose();
        hotkeyService.Dispose();

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _disposed = true;
    }

    public Task RefreshFavoriteHotkeysAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return favoriteHotkeyController.RefreshFavoriteHotkeysAsync(cancellationToken);
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Quick Clipboard",
            ContextMenuStrip = CreateTrayMenu(),
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => OpenClipboard();
    }

    private Forms.ContextMenuStrip CreateTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(CreateMenuItem("打开剪贴板", (_, _) => OpenClipboard()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(CreateMenuItem("暂停 10 分钟", (_, _) => PauseRecordingFor(TimeSpan.FromMinutes(10))));
        menu.Items.Add(CreateMenuItem("暂停 1 小时", (_, _) => PauseRecordingFor(TimeSpan.FromHours(1))));
        menu.Items.Add(CreateMenuItem("暂停直到手动恢复", (_, _) => PauseRecordingIndefinitely()));
        menu.Items.Add(CreateMenuItem("恢复记录", (_, _) => ResumeRecording()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(CreateMenuItem("清空历史", (_, _) => ClearHistory()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(CreateMenuItem("退出", (_, _) => Exit()));
        return menu;
    }

    private static Forms.ToolStripMenuItem CreateMenuItem(string text, EventHandler onClick)
    {
        var item = new Forms.ToolStripMenuItem(text);
        item.Click += onClick;
        return item;
    }

    private void RegisterPanelHotkey(AppSettings settings)
    {
        hotkeyService.HotkeyPressed += OnHotkeyPressed;

        if (!Hotkey.TryParse(settings.PanelHotkey, out var hotkey) || hotkey is null)
        {
            Debug.WriteLine($"Panel hotkey '{settings.PanelHotkey}' could not be parsed.");
            return;
        }

        try
        {
            if (!hotkeyService.Register(PanelHotkeyId, hotkey))
            {
                Debug.WriteLine($"Panel hotkey '{settings.PanelHotkey}' could not be registered.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Panel hotkey registration failed: {ex}");
        }
    }

    private void OnHotkeyPressed(object? sender, string hotkeyId)
    {
        if (string.Equals(hotkeyId, PanelHotkeyId, StringComparison.Ordinal))
        {
            OpenClipboard();
            return;
        }

        if (favoriteHotkeyController.IsFavoriteHotkeyId(hotkeyId))
        {
            FireAndForget(
                favoriteHotkeyController.HandleHotkeyPressedAsync(hotkeyId),
                "Failed to insert favorite hotkey text.");
        }
    }

    private void OpenClipboard()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            floatingPanelLauncher.Open();
            return;
        }

        dispatcher.Invoke(floatingPanelLauncher.Open);
    }

    private void PauseRecordingFor(TimeSpan duration)
    {
        var pausedUntil = clock.Now.Add(duration);
        FireAndForget(
            SaveSettingsAsync(_settings with
            {
                PauseRecordingUntil = pausedUntil,
                PauseRecordingIndefinitely = false
            }),
            "Failed to pause recording.");
    }

    private void PauseRecordingIndefinitely()
    {
        FireAndForget(
            SaveSettingsAsync(_settings with
            {
                PauseRecordingUntil = null,
                PauseRecordingIndefinitely = true
            }),
            "Failed to pause recording indefinitely.");
    }

    private void ResumeRecording()
    {
        FireAndForget(
            SaveSettingsAsync(_settings with
            {
                PauseRecordingUntil = null,
                PauseRecordingIndefinitely = false
            }),
            "Failed to resume recording.");
    }

    private async Task SaveSettingsAsync(AppSettings settings)
    {
        var normalized = NormalizeSettings(settings);

        await _settingsGate.WaitAsync();
        try
        {
            await settingsRepository.SaveAsync(normalized);
            _settings = normalized;
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    private async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var normalized = NormalizeSettings(settings);

        await _settingsGate.WaitAsync(cancellationToken);
        try
        {
            await settingsRepository.SaveAsync(normalized, cancellationToken);
            _settings = normalized;
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    private void ClearHistory()
    {
        FireAndForget(clipboardRepository.ClearClipboardItemsAsync(), "Failed to clear clipboard history.");
    }

    private static void Exit()
    {
        Application.Current.Shutdown();
    }

    private void OnTextCopied(object? sender, string text)
    {
        FireAndForget(RecordCopiedTextAsync(text), "Failed to record copied text.");
    }

    private async Task RecordCopiedTextAsync(string text)
    {
        await _recordingGate.WaitAsync();
        try
        {
            var settings = NormalizeSettings(_settings);
            var now = clock.Now;
            if (IsPaused(settings, now))
            {
                return;
            }

            var latestHash = await clipboardRepository.GetLatestClipboardHashAsync();
            var decision = capturePolicy.Evaluate(text, latestHash, settings);
            if (!decision.Accepted)
            {
                return;
            }

            var item = new ClipboardItem(
                Guid.NewGuid(),
                decision.NormalizedContent!,
                decision.ContentHash!,
                "text",
                now,
                LastUsedAt: null,
                UseCount: 0,
                SourceApp: null);

            await clipboardRepository.AddClipboardItemAsync(item, settings.HistoryRetentionCount);
        }
        finally
        {
            _recordingGate.Release();
        }
    }

    private static bool IsPaused(AppSettings settings, DateTimeOffset now)
    {
        return settings.PauseRecordingIndefinitely
            || (settings.PauseRecordingUntil is not null && settings.PauseRecordingUntil > now);
    }

    private static AppSettings NormalizeSettings(AppSettings settings)
    {
        var defaults = AppSettings.Defaults;

        return settings with
        {
            PanelHotkey = string.IsNullOrWhiteSpace(settings.PanelHotkey)
                ? defaults.PanelHotkey
                : settings.PanelHotkey,
            HistoryRetentionCount = settings.HistoryRetentionCount <= 0
                ? defaults.HistoryRetentionCount
                : settings.HistoryRetentionCount,
            MaximumTextLength = settings.MaximumTextLength <= 0
                ? defaults.MaximumTextLength
                : settings.MaximumTextLength
        };
    }

    private static void FireAndForget(Task task, string errorMessage)
    {
        _ = RunSafelyAsync(task, errorMessage);
    }

    private static async Task RunSafelyAsync(Task task, string errorMessage)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{errorMessage} {ex}");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
