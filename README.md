# Quick Clipboard

Quick Clipboard 是一个 Windows 原生剪贴板助手。它常驻系统托盘，在本地记录最近的纯文本剪贴板历史，并可通过 `Ctrl+Alt+V` 打开悬浮面板。

## MVP 功能

- 纯文本剪贴板历史。
- 常用片段收藏。
- 收藏项全局快捷键。
- 历史/收藏悬浮面板。
- 本地 SQLite 存储。
- 敏感文本过滤。
- 暂停记录和清空历史控制。

## 开发

```powershell
dotnet restore QuickClipboard.sln
dotnet build QuickClipboard.sln
dotnet test QuickClipboard.sln
dotnet run --project src/QuickClipboard.App/QuickClipboard.App.csproj
```

手动集成检查见 `docs/manual-test-checklist.md`。
