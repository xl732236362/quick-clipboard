@echo off
setlocal
chcp 65001 >nul

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\QuickClipboard.App\QuickClipboard.App.csproj"

echo 正在停止 Quick Clipboard...
taskkill /IM QuickClipboard.App.exe /F >nul 2>nul

echo 正在构建 Quick Clipboard...
dotnet build "%ROOT%QuickClipboard.sln"
if errorlevel 1 (
    echo.
    echo 构建失败，未重启 Quick Clipboard。
    pause
    exit /b 1
)

echo 正在启动 Quick Clipboard...
start "" dotnet run --no-build --project "%PROJECT%"

echo 完成。
endlocal
