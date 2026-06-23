# Quick Clipboard MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first usable Windows-native Quick Clipboard MVP: tray-resident WPF app, text clipboard history, favorites, floating panel, global hotkeys, SQLite persistence, privacy filtering, and temporary-clipboard insertion.

**Architecture:** Use a single WPF process with three projects: `QuickClipboard.Core` for domain logic, `QuickClipboard.Infrastructure` for SQLite and Win32 integration, and `QuickClipboard.App` for WPF UI and process lifecycle. Keep Win32/UI Automation code behind interfaces so core logic and view models remain testable.

**Tech Stack:** .NET 9, WPF, C#, `Microsoft.Data.Sqlite`, `CommunityToolkit.Mvvm`, xUnit, FluentAssertions, Win32 interop, UI Automation.

---

## File Structure

Create this structure during implementation:

```text
QuickClipboard.sln
Directory.Build.props
.gitignore
src/
  QuickClipboard.Core/
    QuickClipboard.Core.csproj
    Clipboard/
      ClipboardCaptureDecision.cs
      ClipboardCapturePolicy.cs
      ContentHash.cs
      SensitiveTextDetector.cs
      TextNormalizer.cs
    Favorites/
      FavoriteHotkeyStatus.cs
    Hotkeys/
      Hotkey.cs
      HotkeyModifiers.cs
    Models/
      AppSettings.cs
      ClipboardItem.cs
      FavoriteItem.cs
    Services/
      IClipboardRepository.cs
      IClock.cs
      IContentHasher.cs
      ISensitiveTextDetector.cs
      ISettingsRepository.cs
      ITextNormalizer.cs
  QuickClipboard.Infrastructure/
    QuickClipboard.Infrastructure.csproj
    Persistence/
      AppDataPathProvider.cs
      DatabaseInitializer.cs
      SqliteClipboardRepository.cs
      SqliteConnectionFactory.cs
      SqliteSettingsRepository.cs
    Windows/
      ClipboardMonitor.cs
      GlobalHotkeyService.cs
      NativeMethods.cs
      PanelPositionService.cs
      TextInsertionService.cs
      WindowsClock.cs
  QuickClipboard.App/
    QuickClipboard.App.csproj
    App.xaml
    App.xaml.cs
    Bootstrapper.cs
    Presentation/
      FloatingPanelWindow.xaml
      FloatingPanelWindow.xaml.cs
      MainResources.xaml
      ViewModels/
        FavoriteEditorViewModel.cs
        FloatingPanelViewModel.cs
        ClipboardItemViewModel.cs
        FavoriteItemViewModel.cs
    Tray/
      TrayApplicationService.cs
tests/
  QuickClipboard.Core.Tests/
    QuickClipboard.Core.Tests.csproj
    ClipboardCapturePolicyTests.cs
    ContentHashTests.cs
    HotkeyTests.cs
    SensitiveTextDetectorTests.cs
  QuickClipboard.Infrastructure.Tests/
    QuickClipboard.Infrastructure.Tests.csproj
    DatabaseInitializerTests.cs
    SqliteClipboardRepositoryTests.cs
    SqliteSettingsRepositoryTests.cs
docs/
  manual-test-checklist.md
```

## Task 1: Scaffold Solution And Projects

**Files:**
- Create: `QuickClipboard.sln`
- Create: `Directory.Build.props`
- Create: `.gitignore`
- Create: `src/QuickClipboard.Core/QuickClipboard.Core.csproj`
- Create: `src/QuickClipboard.Infrastructure/QuickClipboard.Infrastructure.csproj`
- Create: `src/QuickClipboard.App/QuickClipboard.App.csproj`
- Create: `tests/QuickClipboard.Core.Tests/QuickClipboard.Core.Tests.csproj`
- Create: `tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

Run:

```powershell
dotnet new sln -n QuickClipboard
dotnet new classlib -n QuickClipboard.Core -o src/QuickClipboard.Core -f net9.0
dotnet new classlib -n QuickClipboard.Infrastructure -o src/QuickClipboard.Infrastructure -f net9.0-windows
dotnet new wpf -n QuickClipboard.App -o src/QuickClipboard.App -f net9.0-windows
dotnet new xunit -n QuickClipboard.Core.Tests -o tests/QuickClipboard.Core.Tests -f net9.0
dotnet new xunit -n QuickClipboard.Infrastructure.Tests -o tests/QuickClipboard.Infrastructure.Tests -f net9.0-windows
dotnet sln QuickClipboard.sln add src/QuickClipboard.Core/QuickClipboard.Core.csproj
dotnet sln QuickClipboard.sln add src/QuickClipboard.Infrastructure/QuickClipboard.Infrastructure.csproj
dotnet sln QuickClipboard.sln add src/QuickClipboard.App/QuickClipboard.App.csproj
dotnet sln QuickClipboard.sln add tests/QuickClipboard.Core.Tests/QuickClipboard.Core.Tests.csproj
dotnet sln QuickClipboard.sln add tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj
dotnet add src/QuickClipboard.Infrastructure/QuickClipboard.Infrastructure.csproj reference src/QuickClipboard.Core/QuickClipboard.Core.csproj
dotnet add src/QuickClipboard.App/QuickClipboard.App.csproj reference src/QuickClipboard.Core/QuickClipboard.Core.csproj
dotnet add src/QuickClipboard.App/QuickClipboard.App.csproj reference src/QuickClipboard.Infrastructure/QuickClipboard.Infrastructure.csproj
dotnet add tests/QuickClipboard.Core.Tests/QuickClipboard.Core.Tests.csproj reference src/QuickClipboard.Core/QuickClipboard.Core.csproj
dotnet add tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj reference src/QuickClipboard.Core/QuickClipboard.Core.csproj
dotnet add tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj reference src/QuickClipboard.Infrastructure/QuickClipboard.Infrastructure.csproj
```

Expected: all commands exit with code 0.

- [ ] **Step 2: Add dependencies**

Run:

```powershell
dotnet add src/QuickClipboard.Infrastructure/QuickClipboard.Infrastructure.csproj package Microsoft.Data.Sqlite
dotnet add src/QuickClipboard.App/QuickClipboard.App.csproj package CommunityToolkit.Mvvm
dotnet add src/QuickClipboard.App/QuickClipboard.App.csproj package Microsoft.Extensions.DependencyInjection
dotnet add tests/QuickClipboard.Core.Tests/QuickClipboard.Core.Tests.csproj package FluentAssertions
dotnet add tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj package FluentAssertions
dotnet add tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj package Microsoft.Data.Sqlite
```

Expected: package restore succeeds.

- [ ] **Step 3: Add shared build settings**

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest</AnalysisLevel>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Enable Windows Forms support in the WPF app**

Modify `src/QuickClipboard.App/QuickClipboard.App.csproj` so its main property group includes:

```xml
<UseWPF>true</UseWPF>
<UseWindowsForms>true</UseWindowsForms>
```

Expected: the app can use WPF windows and `System.Windows.Forms.NotifyIcon`.

- [ ] **Step 5: Add `.gitignore`**

Create `.gitignore`:

```gitignore
bin/
obj/
.vs/
.idea/
*.user
*.suo
TestResults/
.superpowers/
```

- [ ] **Step 6: Remove template placeholder files**

Delete generated `Class1.cs` files from `src/QuickClipboard.Core` and `src/QuickClipboard.Infrastructure`. Delete generated `UnitTest1.cs` files from both test projects.

- [ ] **Step 7: Verify solution builds**

Run:

```powershell
dotnet build QuickClipboard.sln
```

Expected: build succeeds with 0 warnings and 0 errors.

- [ ] **Step 8: Commit scaffold**

Run:

```powershell
git add QuickClipboard.sln Directory.Build.props .gitignore src tests
git commit -m "chore: scaffold quick clipboard solution"
```

## Task 2: Domain Models And Hotkey Parsing

**Files:**
- Create: `src/QuickClipboard.Core/Models/ClipboardItem.cs`
- Create: `src/QuickClipboard.Core/Models/FavoriteItem.cs`
- Create: `src/QuickClipboard.Core/Models/AppSettings.cs`
- Create: `src/QuickClipboard.Core/Hotkeys/HotkeyModifiers.cs`
- Create: `src/QuickClipboard.Core/Hotkeys/Hotkey.cs`
- Create: `tests/QuickClipboard.Core.Tests/HotkeyTests.cs`

- [ ] **Step 1: Write failing hotkey tests**

Create `tests/QuickClipboard.Core.Tests/HotkeyTests.cs`:

```csharp
using FluentAssertions;
using QuickClipboard.Core.Hotkeys;

namespace QuickClipboard.Core.Tests;

public sealed class HotkeyTests
{
    [Fact]
    public void TryParse_ParsesDefaultPanelHotkey()
    {
        var success = Hotkey.TryParse("Ctrl+Alt+V", out var hotkey);

        success.Should().BeTrue();
        hotkey.Should().NotBeNull();
        hotkey!.Modifiers.Should().Be(HotkeyModifiers.Control | HotkeyModifiers.Alt);
        hotkey.Key.Should().Be("V");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl")]
    [InlineData("V")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Alt+")]
    [InlineData("Ctrl+Foo+V")]
    public void TryParse_RejectsInvalidInput(string value)
    {
        Hotkey.TryParse(value, out var hotkey).Should().BeFalse();
        hotkey.Should().BeNull();
    }

    [Fact]
    public void ToString_UsesStableSerialization()
    {
        var hotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift, "D");

        hotkey.ToString().Should().Be("Ctrl+Shift+D");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests/QuickClipboard.Core.Tests/QuickClipboard.Core.Tests.csproj --filter HotkeyTests
```

Expected: FAIL because `QuickClipboard.Core.Hotkeys` types do not exist.

- [ ] **Step 3: Add domain models and hotkey types**

Create `src/QuickClipboard.Core/Hotkeys/HotkeyModifiers.cs`:

```csharp
namespace QuickClipboard.Core.Hotkeys;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}
```

Create `src/QuickClipboard.Core/Hotkeys/Hotkey.cs`:

```csharp
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

        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
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
        if (key.Length == 0 || modifiers == HotkeyModifiers.None)
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
```

Create `src/QuickClipboard.Core/Models/ClipboardItem.cs`:

```csharp
namespace QuickClipboard.Core.Models;

public sealed record ClipboardItem(
    Guid Id,
    string Content,
    string ContentHash,
    string ContentType,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    int UseCount,
    string? SourceApp);
```

Create `src/QuickClipboard.Core/Models/FavoriteItem.cs`:

```csharp
namespace QuickClipboard.Core.Models;

public sealed record FavoriteItem(
    Guid Id,
    string Title,
    string Content,
    string? Hotkey,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastUsedAt,
    int UseCount);
```

Create `src/QuickClipboard.Core/Models/AppSettings.cs`:

```csharp
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
```

- [ ] **Step 4: Run hotkey tests**

Run:

```powershell
dotnet test tests/QuickClipboard.Core.Tests/QuickClipboard.Core.Tests.csproj --filter HotkeyTests
```

Expected: PASS.

- [ ] **Step 5: Commit domain model work**

Run:

```powershell
git add src/QuickClipboard.Core tests/QuickClipboard.Core.Tests
git commit -m "feat: add domain models and hotkey parsing"
```

## Task 3: Text Normalization, Hashing, And Sensitive Filtering

**Files:**
- Create: `src/QuickClipboard.Core/Services/ITextNormalizer.cs`
- Create: `src/QuickClipboard.Core/Services/IContentHasher.cs`
- Create: `src/QuickClipboard.Core/Services/ISensitiveTextDetector.cs`
- Create: `src/QuickClipboard.Core/Clipboard/TextNormalizer.cs`
- Create: `src/QuickClipboard.Core/Clipboard/ContentHash.cs`
- Create: `src/QuickClipboard.Core/Clipboard/SensitiveTextDetector.cs`
- Create: `tests/QuickClipboard.Core.Tests/ContentHashTests.cs`
- Create: `tests/QuickClipboard.Core.Tests/SensitiveTextDetectorTests.cs`

- [ ] **Step 1: Write failing hashing tests**

Create `tests/QuickClipboard.Core.Tests/ContentHashTests.cs`:

```csharp
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
```

- [ ] **Step 2: Write failing sensitive-filter tests**

Create `tests/QuickClipboard.Core.Tests/SensitiveTextDetectorTests.cs`:

```csharp
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
```

- [ ] **Step 3: Run tests to verify they fail**

Run:

```powershell
dotnet test tests/QuickClipboard.Core.Tests/QuickClipboard.Core.Tests.csproj --filter "ContentHashTests|SensitiveTextDetectorTests"
```

Expected: FAIL because filtering and hashing types do not exist.

- [ ] **Step 4: Add service interfaces**

Create `src/QuickClipboard.Core/Services/ITextNormalizer.cs`:

```csharp
namespace QuickClipboard.Core.Services;

public interface ITextNormalizer
{
    string Normalize(string value);
}
```

Create `src/QuickClipboard.Core/Services/IContentHasher.cs`:

```csharp
namespace QuickClipboard.Core.Services;

public interface IContentHasher
{
    string Compute(string value);
}
```

Create `src/QuickClipboard.Core/Services/ISensitiveTextDetector.cs`:

```csharp
namespace QuickClipboard.Core.Services;

public interface ISensitiveTextDetector
{
    bool IsSensitive(string value);
}
```

- [ ] **Step 5: Add text utilities**

Create `src/QuickClipboard.Core/Clipboard/TextNormalizer.cs`:

```csharp
using QuickClipboard.Core.Services;

namespace QuickClipboard.Core.Clipboard;

public sealed class TextNormalizer : ITextNormalizer
{
    public string Normalize(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }
}
```

Create `src/QuickClipboard.Core/Clipboard/ContentHash.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using QuickClipboard.Core.Services;

namespace QuickClipboard.Core.Clipboard;

public sealed class ContentHash(ITextNormalizer normalizer) : IContentHasher
{
    public string Compute(string value)
    {
        var normalized = normalizer.Normalize(value);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
```

Create `src/QuickClipboard.Core/Clipboard/SensitiveTextDetector.cs`:

```csharp
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
```

- [ ] **Step 6: Run filtering and hash tests**

Run:

```powershell
dotnet test tests/QuickClipboard.Core.Tests/QuickClipboard.Core.Tests.csproj --filter "ContentHashTests|SensitiveTextDetectorTests"
```

Expected: PASS.

- [ ] **Step 7: Commit filtering work**

Run:

```powershell
git add src/QuickClipboard.Core tests/QuickClipboard.Core.Tests
git commit -m "feat: add text filtering and hashing"
```

## Task 4: Clipboard Capture Policy

**Files:**
- Create: `src/QuickClipboard.Core/Clipboard/ClipboardCaptureDecision.cs`
- Create: `src/QuickClipboard.Core/Clipboard/ClipboardCapturePolicy.cs`
- Create: `tests/QuickClipboard.Core.Tests/ClipboardCapturePolicyTests.cs`

- [ ] **Step 1: Write failing capture policy tests**

Create `tests/QuickClipboard.Core.Tests/ClipboardCapturePolicyTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests/QuickClipboard.Core.Tests/QuickClipboard.Core.Tests.csproj --filter ClipboardCapturePolicyTests
```

Expected: FAIL because `ClipboardCapturePolicy` does not exist.

- [ ] **Step 3: Add capture decision and policy**

Create `src/QuickClipboard.Core/Clipboard/ClipboardCaptureDecision.cs`:

```csharp
namespace QuickClipboard.Core.Clipboard;

public sealed record ClipboardCaptureDecision(
    bool Accepted,
    string? NormalizedContent,
    string? ContentHash,
    string? Reason)
{
    public static ClipboardCaptureDecision Accept(string content, string hash)
    {
        return new ClipboardCaptureDecision(true, content, hash, null);
    }

    public static ClipboardCaptureDecision Reject(string reason)
    {
        return new ClipboardCaptureDecision(false, null, null, reason);
    }
}
```

Create `src/QuickClipboard.Core/Clipboard/ClipboardCapturePolicy.cs`:

```csharp
using QuickClipboard.Core.Models;
using QuickClipboard.Core.Services;

namespace QuickClipboard.Core.Clipboard;

public sealed class ClipboardCapturePolicy(
    ITextNormalizer normalizer,
    IContentHasher hasher,
    ISensitiveTextDetector sensitiveTextDetector)
{
    public ClipboardCaptureDecision Evaluate(string? text, string? latestContentHash, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ClipboardCaptureDecision.Reject("empty");
        }

        var normalized = normalizer.Normalize(text);
        if (normalized.Length == 0)
        {
            return ClipboardCaptureDecision.Reject("empty");
        }

        if (normalized.Length > settings.MaximumTextLength)
        {
            return ClipboardCaptureDecision.Reject("too_long");
        }

        if (sensitiveTextDetector.IsSensitive(normalized))
        {
            return ClipboardCaptureDecision.Reject("sensitive");
        }

        var hash = hasher.Compute(normalized);
        if (string.Equals(hash, latestContentHash, StringComparison.OrdinalIgnoreCase))
        {
            return ClipboardCaptureDecision.Reject("duplicate");
        }

        return ClipboardCaptureDecision.Accept(normalized, hash);
    }
}
```

- [ ] **Step 4: Run policy tests**

Run:

```powershell
dotnet test tests/QuickClipboard.Core.Tests/QuickClipboard.Core.Tests.csproj --filter ClipboardCapturePolicyTests
```

Expected: PASS.

- [ ] **Step 5: Commit capture policy**

Run:

```powershell
git add src/QuickClipboard.Core tests/QuickClipboard.Core.Tests
git commit -m "feat: add clipboard capture policy"
```

## Task 5: Repository Interfaces And SQLite Schema

**Files:**
- Create: `src/QuickClipboard.Core/Services/IClipboardRepository.cs`
- Create: `src/QuickClipboard.Core/Services/ISettingsRepository.cs`
- Create: `src/QuickClipboard.Core/Services/IClock.cs`
- Create: `src/QuickClipboard.Infrastructure/Windows/WindowsClock.cs`
- Create: `src/QuickClipboard.Infrastructure/Persistence/SqliteConnectionFactory.cs`
- Create: `src/QuickClipboard.Infrastructure/Persistence/DatabaseInitializer.cs`
- Create: `tests/QuickClipboard.Infrastructure.Tests/DatabaseInitializerTests.cs`

- [ ] **Step 1: Write failing database initializer test**

Create `tests/QuickClipboard.Infrastructure.Tests/DatabaseInitializerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Data.Sqlite;
using QuickClipboard.Infrastructure.Persistence;

namespace QuickClipboard.Infrastructure.Tests;

public sealed class DatabaseInitializerTests
{
    [Fact]
    public async Task InitializeAsync_CreatesRequiredTables()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var initializer = new DatabaseInitializer(() => connection);

        await initializer.InitializeAsync();

        var tableNames = await ReadTableNames(connection);
        tableNames.Should().Contain(["clipboard_items", "favorites", "settings"]);
    }

    private static async Task<IReadOnlyList<string>> ReadTableNames(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj --filter DatabaseInitializerTests
```

Expected: FAIL because persistence types do not exist.

- [ ] **Step 3: Add repository interfaces**

Create `src/QuickClipboard.Core/Services/IClock.cs`:

```csharp
namespace QuickClipboard.Core.Services;

public interface IClock
{
    DateTimeOffset Now { get; }
}
```

Create `src/QuickClipboard.Core/Services/IClipboardRepository.cs`:

```csharp
using QuickClipboard.Core.Models;

namespace QuickClipboard.Core.Services;

public interface IClipboardRepository
{
    Task<string?> GetLatestClipboardHashAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClipboardItem>> GetRecentClipboardItemsAsync(int limit, CancellationToken cancellationToken = default);
    Task AddClipboardItemAsync(ClipboardItem item, int retentionLimit, CancellationToken cancellationToken = default);
    Task DeleteClipboardItemAsync(Guid id, CancellationToken cancellationToken = default);
    Task ClearClipboardItemsAsync(CancellationToken cancellationToken = default);
    Task MarkClipboardItemUsedAsync(Guid id, DateTimeOffset usedAt, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(CancellationToken cancellationToken = default);
    Task AddFavoriteAsync(FavoriteItem item, CancellationToken cancellationToken = default);
    Task UpdateFavoriteAsync(FavoriteItem item, CancellationToken cancellationToken = default);
    Task DeleteFavoriteAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkFavoriteUsedAsync(Guid id, DateTimeOffset usedAt, CancellationToken cancellationToken = default);
}
```

Create `src/QuickClipboard.Core/Services/ISettingsRepository.cs`:

```csharp
using QuickClipboard.Core.Models;

namespace QuickClipboard.Core.Services;

public interface ISettingsRepository
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
```

Create `src/QuickClipboard.Infrastructure/Windows/WindowsClock.cs`:

```csharp
using QuickClipboard.Core.Services;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class WindowsClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
```

- [ ] **Step 4: Add SQLite connection and initializer**

Create `src/QuickClipboard.Infrastructure/Persistence/SqliteConnectionFactory.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace QuickClipboard.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory(string databasePath)
{
    public SqliteConnection CreateConnection()
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return new SqliteConnection($"Data Source={databasePath}");
    }
}
```

Create `src/QuickClipboard.Infrastructure/Persistence/DatabaseInitializer.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace QuickClipboard.Infrastructure.Persistence;

public sealed class DatabaseInitializer(Func<SqliteConnection> createConnection)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = createConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await ExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS clipboard_items (
                id TEXT PRIMARY KEY,
                content TEXT NOT NULL,
                content_hash TEXT NOT NULL,
                content_type TEXT NOT NULL,
                created_at TEXT NOT NULL,
                last_used_at TEXT NULL,
                use_count INTEGER NOT NULL DEFAULT 0,
                source_app TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_clipboard_items_created_at
                ON clipboard_items(created_at DESC);
            CREATE TABLE IF NOT EXISTS favorites (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                content TEXT NOT NULL,
                hotkey TEXT NULL,
                sort_order INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                last_used_at TEXT NULL,
                use_count INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_favorites_sort_order
                ON favorites(sort_order ASC);
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """, cancellationToken);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string commandText, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Run database initializer test**

Run:

```powershell
dotnet test tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj --filter DatabaseInitializerTests
```

Expected: PASS.

- [ ] **Step 6: Commit schema work**

Run:

```powershell
git add src/QuickClipboard.Core src/QuickClipboard.Infrastructure tests/QuickClipboard.Infrastructure.Tests
git commit -m "feat: add sqlite schema initialization"
```

## Task 6: SQLite Clipboard And Favorites Repository

**Files:**
- Create: `src/QuickClipboard.Infrastructure/Persistence/SqliteClipboardRepository.cs`
- Create: `tests/QuickClipboard.Infrastructure.Tests/SqliteClipboardRepositoryTests.cs`

- [ ] **Step 1: Write failing repository tests**

Create `tests/QuickClipboard.Infrastructure.Tests/SqliteClipboardRepositoryTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Data.Sqlite;
using QuickClipboard.Core.Models;
using QuickClipboard.Infrastructure.Persistence;

namespace QuickClipboard.Infrastructure.Tests;

public sealed class SqliteClipboardRepositoryTests
{
    [Fact]
    public async Task AddClipboardItemAsync_StoresItemsAndTrimsOldHistory()
    {
        await using var connection = await CreateInitializedConnectionAsync();
        var repository = new SqliteClipboardRepository(() => connection);

        for (var i = 0; i < 3; i++)
        {
            await repository.AddClipboardItemAsync(CreateClipboardItem($"item {i}", DateTimeOffset.UtcNow.AddMinutes(i)), retentionLimit: 2);
        }

        var items = await repository.GetRecentClipboardItemsAsync(10);

        items.Select(item => item.Content).Should().Equal("item 2", "item 1");
    }

    [Fact]
    public async Task Favorites_CanBeCreatedUpdatedDeletedAndMarkedUsed()
    {
        await using var connection = await CreateInitializedConnectionAsync();
        var repository = new SqliteClipboardRepository(() => connection);
        var now = DateTimeOffset.UtcNow;
        var favorite = new FavoriteItem(Guid.NewGuid(), "Email", "hello@example.com", "Ctrl+Alt+E", 10, now, now, null, 0);

        await repository.AddFavoriteAsync(favorite);
        await repository.UpdateFavoriteAsync(favorite with { Title = "Work Email", SortOrder = 1 });
        await repository.MarkFavoriteUsedAsync(favorite.Id, now.AddMinutes(1));

        var favorites = await repository.GetFavoritesAsync();
        favorites.Should().ContainSingle();
        favorites[0].Title.Should().Be("Work Email");
        favorites[0].UseCount.Should().Be(1);
        favorites[0].LastUsedAt.Should().Be(now.AddMinutes(1));

        await repository.DeleteFavoriteAsync(favorite.Id);
        (await repository.GetFavoritesAsync()).Should().BeEmpty();
    }

    private static ClipboardItem CreateClipboardItem(string content, DateTimeOffset createdAt)
    {
        return new ClipboardItem(Guid.NewGuid(), content, content, "text", createdAt, null, 0, null);
    }

    private static async Task<SqliteConnection> CreateInitializedConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await new DatabaseInitializer(() => connection).InitializeAsync();
        return connection;
    }
}
```

- [ ] **Step 2: Run repository tests to verify they fail**

Run:

```powershell
dotnet test tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj --filter SqliteClipboardRepositoryTests
```

Expected: FAIL because `SqliteClipboardRepository` does not exist.

- [ ] **Step 3: Implement `SqliteClipboardRepository`**

Create `src/QuickClipboard.Infrastructure/Persistence/SqliteClipboardRepository.cs` with these public members:

```csharp
using Microsoft.Data.Sqlite;
using QuickClipboard.Core.Models;
using QuickClipboard.Core.Services;

namespace QuickClipboard.Infrastructure.Persistence;

public sealed class SqliteClipboardRepository(Func<SqliteConnection> createConnection) : IClipboardRepository
{
    public Task<string?> GetLatestClipboardHashAsync(CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<ClipboardItem>> GetRecentClipboardItemsAsync(int limit, CancellationToken cancellationToken = default);
    public Task AddClipboardItemAsync(ClipboardItem item, int retentionLimit, CancellationToken cancellationToken = default);
    public Task DeleteClipboardItemAsync(Guid id, CancellationToken cancellationToken = default);
    public Task ClearClipboardItemsAsync(CancellationToken cancellationToken = default);
    public Task MarkClipboardItemUsedAsync(Guid id, DateTimeOffset usedAt, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(CancellationToken cancellationToken = default);
    public Task AddFavoriteAsync(FavoriteItem item, CancellationToken cancellationToken = default);
    public Task UpdateFavoriteAsync(FavoriteItem item, CancellationToken cancellationToken = default);
    public Task DeleteFavoriteAsync(Guid id, CancellationToken cancellationToken = default);
    public Task MarkFavoriteUsedAsync(Guid id, DateTimeOffset usedAt, CancellationToken cancellationToken = default);
}
```

Implementation requirements:

- Open the connection if it is not already open.
- Store `Guid` as `TEXT` using `id.ToString("D")`.
- Store timestamps using `DateTimeOffset.ToString("O")`.
- Read timestamps using `DateTimeOffset.Parse(value, CultureInfo.InvariantCulture)`.
- Return clipboard history ordered by `created_at DESC`.
- Return favorites ordered by `sort_order ASC, created_at ASC`.
- After inserting a history item, trim rows not in the newest `retentionLimit` rows:

```sql
DELETE FROM clipboard_items
WHERE id NOT IN (
    SELECT id FROM clipboard_items
    ORDER BY created_at DESC
    LIMIT $limit
);
```

- [ ] **Step 4: Run repository tests**

Run:

```powershell
dotnet test tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj --filter SqliteClipboardRepositoryTests
```

Expected: PASS.

- [ ] **Step 5: Commit repository work**

Run:

```powershell
git add src/QuickClipboard.Infrastructure tests/QuickClipboard.Infrastructure.Tests
git commit -m "feat: add sqlite clipboard repository"
```

## Task 7: SQLite Settings Repository

**Files:**
- Create: `src/QuickClipboard.Infrastructure/Persistence/SqliteSettingsRepository.cs`
- Create: `tests/QuickClipboard.Infrastructure.Tests/SqliteSettingsRepositoryTests.cs`

- [ ] **Step 1: Write failing settings repository tests**

Create `tests/QuickClipboard.Infrastructure.Tests/SqliteSettingsRepositoryTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Data.Sqlite;
using QuickClipboard.Core.Models;
using QuickClipboard.Infrastructure.Persistence;

namespace QuickClipboard.Infrastructure.Tests;

public sealed class SqliteSettingsRepositoryTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefaultsWhenSettingsAreMissing()
    {
        await using var connection = await CreateInitializedConnectionAsync();
        var repository = new SqliteSettingsRepository(() => connection);

        var settings = await repository.LoadAsync();

        settings.Should().Be(AppSettings.Defaults);
    }

    [Fact]
    public async Task SaveAsync_PersistsSettings()
    {
        await using var connection = await CreateInitializedConnectionAsync();
        var repository = new SqliteSettingsRepository(() => connection);
        var expected = AppSettings.Defaults with
        {
            PanelHotkey = "Ctrl+Shift+Space",
            HistoryRetentionCount = 50,
            MaximumTextLength = 1000,
            PauseRecordingUntil = DateTimeOffset.Parse("2026-06-23T12:00:00Z"),
            PauseRecordingIndefinitely = true
        };

        await repository.SaveAsync(expected);
        var actual = await repository.LoadAsync();

        actual.Should().Be(expected);
    }

    private static async Task<SqliteConnection> CreateInitializedConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await new DatabaseInitializer(() => connection).InitializeAsync();
        return connection;
    }
}
```

- [ ] **Step 2: Run settings tests to verify they fail**

Run:

```powershell
dotnet test tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj --filter SqliteSettingsRepositoryTests
```

Expected: FAIL because `SqliteSettingsRepository` does not exist.

- [ ] **Step 3: Implement settings repository**

Create `src/QuickClipboard.Infrastructure/Persistence/SqliteSettingsRepository.cs` with:

```csharp
using Microsoft.Data.Sqlite;
using QuickClipboard.Core.Models;
using QuickClipboard.Core.Services;

namespace QuickClipboard.Infrastructure.Persistence;

public sealed class SqliteSettingsRepository(Func<SqliteConnection> createConnection) : ISettingsRepository
{
    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
```

Implementation requirements:

- Persist each setting as one row in `settings`.
- Use keys exactly: `panel_hotkey`, `history_retention_count`, `maximum_text_length`, `pause_recording_until`, `pause_recording_indefinitely`.
- Empty `pause_recording_until` means `null`.
- Parse booleans with `bool.Parse`.
- Parse integers with `int.Parse(value, CultureInfo.InvariantCulture)`.
- Merge missing rows with `AppSettings.Defaults`.

- [ ] **Step 4: Run settings tests**

Run:

```powershell
dotnet test tests/QuickClipboard.Infrastructure.Tests/QuickClipboard.Infrastructure.Tests.csproj --filter SqliteSettingsRepositoryTests
```

Expected: PASS.

- [ ] **Step 5: Commit settings persistence**

Run:

```powershell
git add src/QuickClipboard.Infrastructure tests/QuickClipboard.Infrastructure.Tests
git commit -m "feat: persist app settings"
```

## Task 8: Windows Clipboard Monitor

**Files:**
- Create: `src/QuickClipboard.Infrastructure/Windows/ClipboardMonitor.cs`
- Modify: `src/QuickClipboard.Infrastructure/Windows/NativeMethods.cs`

- [ ] **Step 1: Define monitor behavior**

Create `ClipboardMonitor` with this public API:

```csharp
namespace QuickClipboard.Infrastructure.Windows;

public sealed class ClipboardMonitor : IDisposable
{
    public event EventHandler<string>? TextCopied;

    public void Start();
    public void SuppressNextChanges(TimeSpan duration);
    public void Dispose();
}
```

- [ ] **Step 2: Implement Win32 listener**

Use `AddClipboardFormatListener` and `WM_CLIPBOARDUPDATE` on a hidden `HwndSource`. Add these P/Invoke members to `NativeMethods.cs`:

```csharp
internal static partial class NativeMethods
{
    internal const int WM_CLIPBOARDUPDATE = 0x031D;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AddClipboardFormatListener(IntPtr hwnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RemoveClipboardFormatListener(IntPtr hwnd);
}
```

`ClipboardMonitor` requirements:

- Create the hidden message window on the WPF UI thread.
- On `WM_CLIPBOARDUPDATE`, ignore events while `DateTimeOffset.Now` is before the suppression expiry.
- Read text through `System.Windows.Clipboard.ContainsText()` and `System.Windows.Clipboard.GetText()`.
- Catch `ExternalException` and ignore the event because the clipboard may be locked by another process.
- Raise `TextCopied` with the raw text. Filtering happens in the app service.

- [ ] **Step 3: Manual smoke test**

Temporarily wire `TextCopied` in `App.xaml.cs` to write copied text to `Debug.WriteLine`. Run:

```powershell
dotnet run --project src/QuickClipboard.App/QuickClipboard.App.csproj
```

Copy text from Notepad. Expected: the app receives copied text in the debugger output. Remove the temporary `Debug.WriteLine` before committing.

- [ ] **Step 4: Commit clipboard monitor**

Run:

```powershell
git add src/QuickClipboard.Infrastructure
git commit -m "feat: add windows clipboard monitor"
```

## Task 9: Global Hotkey Service

**Files:**
- Create: `src/QuickClipboard.Core/Favorites/FavoriteHotkeyStatus.cs`
- Create: `src/QuickClipboard.Infrastructure/Windows/GlobalHotkeyService.cs`
- Modify: `src/QuickClipboard.Infrastructure/Windows/NativeMethods.cs`

- [ ] **Step 1: Add hotkey status model**

Create `src/QuickClipboard.Core/Favorites/FavoriteHotkeyStatus.cs`:

```csharp
namespace QuickClipboard.Core.Favorites;

public sealed record FavoriteHotkeyStatus(Guid FavoriteId, string Hotkey, bool IsRegistered, string? Error);
```

- [ ] **Step 2: Define hotkey service API**

Create `GlobalHotkeyService` with this public API:

```csharp
using QuickClipboard.Core.Hotkeys;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class GlobalHotkeyService : IDisposable
{
    public event EventHandler<string>? HotkeyPressed;

    public bool Register(string hotkeyId, Hotkey hotkey);
    public void Unregister(string hotkeyId);
    public void UnregisterAll();
    public void Dispose();
}
```

- [ ] **Step 3: Implement Win32 registration**

Add to `NativeMethods.cs`:

```csharp
[LibraryImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
internal static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

[LibraryImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
internal static partial bool UnregisterHotKey(IntPtr hWnd, int id);
```

Implementation requirements:

- Use a hidden `HwndSource`.
- Map `HotkeyModifiers.Control`, `Alt`, `Shift`, `Windows` to Win32 modifier flags `0x0002`, `0x0001`, `0x0004`, `0x0008`.
- Convert keys with `KeyInterop.VirtualKeyFromKey((Key)new KeyConverter().ConvertFromString(hotkey.Key)!)`.
- Keep a dictionary from generated integer IDs to string hotkey IDs.
- On `WM_HOTKEY`, raise `HotkeyPressed` with the string hotkey ID.
- Return `false` when `RegisterHotKey` fails.

- [ ] **Step 4: Manual hotkey test**

Create a temporary registration for `"panel"` with `Ctrl+Alt+V` and write to `Debug.WriteLine` when pressed. Run:

```powershell
dotnet run --project src/QuickClipboard.App/QuickClipboard.App.csproj
```

Press `Ctrl+Alt+V`. Expected: the debug line fires once per press. Remove the temporary debug code before committing.

- [ ] **Step 5: Commit hotkey service**

Run:

```powershell
git add src/QuickClipboard.Core src/QuickClipboard.Infrastructure
git commit -m "feat: add global hotkey service"
```

## Task 10: Panel Position Service

**Files:**
- Create: `src/QuickClipboard.Infrastructure/Windows/PanelPositionService.cs`
- Modify: `src/QuickClipboard.Infrastructure/Windows/NativeMethods.cs`

- [ ] **Step 1: Define position service API**

Create `PanelPositionService` with:

```csharp
using System.Windows;

namespace QuickClipboard.Infrastructure.Windows;

public sealed class PanelPositionService
{
    public Rect GetPreferredAnchor();
}
```

- [ ] **Step 2: Implement caret-first lookup**

Implementation order:

1. Use `AutomationElement.FocusedElement`.
2. Try `TextPattern` and `TextPatternRange.GetBoundingRectangles()`.
3. If no rectangle is available, use `GetGUIThreadInfo` with the foreground window thread.
4. If both fail, return a small rectangle around `System.Windows.Forms.Control.MousePosition`.

Add these P/Invoke members to `NativeMethods.cs`:

```csharp
[LibraryImport("user32.dll")]
internal static partial IntPtr GetForegroundWindow();

[LibraryImport("user32.dll")]
internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

[LibraryImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
internal static partial bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo info);

[LibraryImport("user32.dll")]
internal static partial bool ClientToScreen(IntPtr hWnd, ref Point point);
```

`GuiThreadInfo` must include `cbSize`, `hwndCaret`, and `rcCaret`. Convert Win32 coordinates to WPF device-independent pixels using the current presentation source transform when possible.

- [ ] **Step 3: Add clamping helper**

Add a method:

```csharp
public Point ClampPanelTopLeft(Rect anchor, Size panelSize)
```

Requirements:

- Find the monitor containing the anchor point through `System.Windows.Forms.Screen.FromPoint`.
- Use `WorkingArea`.
- Place panel below the anchor by default.
- If the panel would overflow bottom, place above.
- Clamp left and top inside working area.

- [ ] **Step 4: Manual position test**

Temporarily open a small WPF window at `ClampPanelTopLeft(GetPreferredAnchor(), new Size(420, 520))`. Test in Notepad and browser input. Expected: the test window appears near the text caret when possible and near the mouse otherwise. Remove temporary code before committing.

- [ ] **Step 5: Commit positioning service**

Run:

```powershell
git add src/QuickClipboard.Infrastructure
git commit -m "feat: add floating panel positioning"
```

## Task 11: Text Insertion Service

**Files:**
- Create: `src/QuickClipboard.Infrastructure/Windows/TextInsertionService.cs`
- Modify: `src/QuickClipboard.Infrastructure/Windows/NativeMethods.cs`

- [ ] **Step 1: Define insertion API**

Create `TextInsertionService` with:

```csharp
namespace QuickClipboard.Infrastructure.Windows;

public sealed class TextInsertionService
{
    public Task InsertTextAsync(string text, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Implement temporary clipboard insertion**

Requirements:

- Read current clipboard data with `System.Windows.Clipboard.GetDataObject()`.
- Set Unicode text with `System.Windows.Clipboard.SetText(text, TextDataFormat.UnicodeText)`.
- Send `Ctrl+V` with `SendInput`.
- Wait 120 milliseconds with `Task.Delay`.
- Restore the original `IDataObject` with `System.Windows.Clipboard.SetDataObject(original, true)` when available.
- Catch clipboard access exceptions and rethrow a user-actionable `InvalidOperationException` with message `"Clipboard is currently unavailable."`.

Add required `SendInput` P/Invoke structures to `NativeMethods.cs`. Keep them internal to infrastructure.

- [ ] **Step 3: Manual insertion test**

Run the app with a temporary command that calls `InsertTextAsync("Quick Clipboard test")` after a hotkey press. Focus Notepad, press the hotkey. Expected: Notepad receives `Quick Clipboard test`, and the previous clipboard content is restored after insertion. Remove temporary command before committing.

- [ ] **Step 4: Commit insertion service**

Run:

```powershell
git add src/QuickClipboard.Infrastructure
git commit -m "feat: add text insertion service"
```

## Task 12: App Bootstrap, Tray Lifecycle, And Recording Pipeline

**Files:**
- Modify: `src/QuickClipboard.App/App.xaml`
- Modify: `src/QuickClipboard.App/App.xaml.cs`
- Create: `src/QuickClipboard.App/Bootstrapper.cs`
- Create: `src/QuickClipboard.App/Tray/TrayApplicationService.cs`
- Create: `src/QuickClipboard.Infrastructure/Persistence/AppDataPathProvider.cs`

- [ ] **Step 1: Add app data path provider**

Create `src/QuickClipboard.Infrastructure/Persistence/AppDataPathProvider.cs`:

```csharp
namespace QuickClipboard.Infrastructure.Persistence;

public static class AppDataPathProvider
{
    public static string GetDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "QuickClipboard", "quick-clipboard.db");
    }
}
```

- [ ] **Step 2: Configure dependency registration**

Create `src/QuickClipboard.App/Bootstrapper.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using QuickClipboard.Core.Clipboard;
using QuickClipboard.Core.Services;
using QuickClipboard.Infrastructure.Persistence;
using QuickClipboard.Infrastructure.Windows;

namespace QuickClipboard.App;

public static class Bootstrapper
{
    public static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        var databasePath = AppDataPathProvider.GetDatabasePath();
        var connectionFactory = new SqliteConnectionFactory(databasePath);

        services.AddSingleton<Func<SqliteConnection>>(_ => connectionFactory.CreateConnection);
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<ITextNormalizer, TextNormalizer>();
        services.AddSingleton<IContentHasher, ContentHash>();
        services.AddSingleton<ISensitiveTextDetector, SensitiveTextDetector>();
        services.AddSingleton<ClipboardCapturePolicy>();
        services.AddSingleton<IClipboardRepository, SqliteClipboardRepository>();
        services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
        services.AddSingleton<IClock, WindowsClock>();
        services.AddSingleton<ClipboardMonitor>();
        services.AddSingleton<GlobalHotkeyService>();
        services.AddSingleton<PanelPositionService>();
        services.AddSingleton<TextInsertionService>();
        services.AddSingleton<TrayApplicationService>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
```

- [ ] **Step 3: Implement tray service**

Create `src/QuickClipboard.App/Tray/TrayApplicationService.cs` with responsibilities:

- Initialize the database.
- Start `ClipboardMonitor`.
- Load settings.
- Register panel hotkey.
- Subscribe to `ClipboardMonitor.TextCopied`.
- For copied text: skip when recording is paused, evaluate `ClipboardCapturePolicy`, and save accepted items.
- Own a `System.Windows.Forms.NotifyIcon` with menu items: `Open Clipboard`, `Pause 10 minutes`, `Pause 1 hour`, `Pause until resumed`, `Resume Recording`, `Clear History`, `Exit`.
- Dispose monitor, hotkeys, and tray icon on exit.

Use this pause check:

```csharp
private static bool IsPaused(AppSettings settings, DateTimeOffset now)
{
    return settings.PauseRecordingIndefinitely
        || (settings.PauseRecordingUntil is not null && settings.PauseRecordingUntil > now);
}
```

- [ ] **Step 4: Wire app startup**

Modify `App.xaml` to remove `StartupUri`. In `App.xaml.cs`, build services in `OnStartup`, resolve `TrayApplicationService`, and call `StartAsync`. Set `ShutdownMode = ShutdownMode.OnExplicitShutdown`.

- [ ] **Step 5: Manual tray and recording test**

Run:

```powershell
dotnet run --project src/QuickClipboard.App/QuickClipboard.App.csproj
```

Expected:

- App starts without a main window.
- Tray icon appears.
- Copying normal text inserts one row into `%AppData%\QuickClipboard\quick-clipboard.db`.
- Pause menu prevents new rows.
- Clear history removes all rows.
- Exit closes the process.

- [ ] **Step 6: Commit app lifecycle**

Run:

```powershell
git add src/QuickClipboard.App src/QuickClipboard.Infrastructure
git commit -m "feat: add tray lifecycle and recording pipeline"
```

## Task 13: Floating Panel View Models

**Files:**
- Create: `src/QuickClipboard.App/Presentation/ViewModels/ClipboardItemViewModel.cs`
- Create: `src/QuickClipboard.App/Presentation/ViewModels/FavoriteItemViewModel.cs`
- Create: `src/QuickClipboard.App/Presentation/ViewModels/FavoriteEditorViewModel.cs`
- Create: `src/QuickClipboard.App/Presentation/ViewModels/FloatingPanelViewModel.cs`

- [ ] **Step 1: Add item view models**

Create lightweight item view models:

```csharp
public sealed partial class ClipboardItemViewModel : ObservableObject
{
    public Guid Id { get; }
    public string Preview { get; }
    public string Content { get; }
    public DateTimeOffset CreatedAt { get; }
}
```

```csharp
public sealed partial class FavoriteItemViewModel : ObservableObject
{
    public Guid Id { get; }
    public string Title { get; }
    public string Preview { get; }
    public string Content { get; }
    public string? Hotkey { get; }
}
```

Preview requirement: replace line breaks with spaces and truncate to 120 characters.

- [ ] **Step 2: Add floating panel view model**

`FloatingPanelViewModel` must expose:

```csharp
public ObservableCollection<ClipboardItemViewModel> HistoryItems { get; }
public ObservableCollection<FavoriteItemViewModel> FavoriteItems { get; }
public IAsyncRelayCommand RefreshCommand { get; }
public IAsyncRelayCommand<ClipboardItemViewModel> PasteHistoryCommand { get; }
public IAsyncRelayCommand<FavoriteItemViewModel> PasteFavoriteCommand { get; }
public IAsyncRelayCommand<ClipboardItemViewModel> AddHistoryToFavoritesCommand { get; }
public IAsyncRelayCommand<ClipboardItemViewModel> DeleteHistoryCommand { get; }
public IAsyncRelayCommand<FavoriteItemViewModel> DeleteFavoriteCommand { get; }
```

Command requirements:

- `RefreshCommand`: load 200 history items and all favorites.
- `PasteHistoryCommand`: call `TextInsertionService.InsertTextAsync`, mark item used, request panel close.
- `PasteFavoriteCommand`: call `TextInsertionService.InsertTextAsync`, mark item used, request panel close.
- `AddHistoryToFavoritesCommand`: create a favorite with title derived from first line, no hotkey, next sort order.
- `DeleteHistoryCommand`: delete item and remove from collection.
- `DeleteFavoriteCommand`: delete item and remove from collection.

- [ ] **Step 3: Add close event**

Add:

```csharp
public event EventHandler? CloseRequested;
```

Raise it after successful paste.

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build QuickClipboard.sln
```

Expected: PASS.

- [ ] **Step 5: Commit view models**

Run:

```powershell
git add src/QuickClipboard.App/Presentation
git commit -m "feat: add floating panel view models"
```

## Task 14: Floating Panel WPF UI

**Files:**
- Create: `src/QuickClipboard.App/Presentation/MainResources.xaml`
- Create: `src/QuickClipboard.App/Presentation/FloatingPanelWindow.xaml`
- Create: `src/QuickClipboard.App/Presentation/FloatingPanelWindow.xaml.cs`
- Modify: `src/QuickClipboard.App/App.xaml`

- [ ] **Step 1: Add shared styles**

Create `MainResources.xaml` with:

- dark translucent panel background `#F21F1F1F`;
- text color `#F5F5F5`;
- muted text color `#B8B8B8`;
- accent color `#4CC2FF`;
- item hover background `#333333`;
- 8px corner radius for list items and buttons.

Merge it in `App.xaml`.

- [ ] **Step 2: Build floating panel XAML**

`FloatingPanelWindow.xaml` requirements:

- `WindowStyle="None"`.
- `ShowInTaskbar="False"`.
- `Topmost="True"`.
- fixed first-pass size `Width="460"` and `MaxHeight="560"`.
- root border with 8px corner radius.
- top `TabControl` with `History` and `Favorites`.
- each tab uses a `ListBox`.
- history item template shows preview, created time, `Paste`, `Favorite`, `Delete`.
- favorite item template shows title, preview, hotkey text, `Paste`, `Delete`.
- bottom small status text for empty state.

- [ ] **Step 3: Add panel code-behind behavior**

`FloatingPanelWindow.xaml.cs` requirements:

- Accept `FloatingPanelViewModel`, `PanelPositionService`, and anchor rectangle in constructor.
- Set `DataContext`.
- On `Loaded`, run `RefreshCommand`, measure panel size, call `ClampPanelTopLeft`, and set `Left`/`Top`.
- Close on `Esc`.
- Close on `Deactivated`.
- Subscribe to `CloseRequested` and close.

- [ ] **Step 4: Hook panel hotkey**

In `TrayApplicationService`, when the panel hotkey fires:

- Resolve a new `FloatingPanelViewModel`.
- Ask `PanelPositionService.GetPreferredAnchor()`.
- Create and show `FloatingPanelWindow`.
- Ensure only one panel is open at a time by closing the previous instance.

- [ ] **Step 5: Manual panel test**

Run:

```powershell
dotnet run --project src/QuickClipboard.App/QuickClipboard.App.csproj
```

Test:

- Copy three text snippets.
- Focus Notepad.
- Press `Ctrl+Alt+V`.
- Confirm panel opens near caret or mouse.
- Click a history item.
- Confirm text appears in Notepad and panel closes.
- Press `Esc`; panel closes.
- Click outside; panel closes.

- [ ] **Step 6: Commit floating panel UI**

Run:

```powershell
git add src/QuickClipboard.App
git commit -m "feat: add floating clipboard panel"
```

## Task 15: Favorite Creation, Editing, And Hotkey Registration

**Files:**
- Modify: `src/QuickClipboard.App/Presentation/ViewModels/FavoriteEditorViewModel.cs`
- Modify: `src/QuickClipboard.App/Presentation/FloatingPanelWindow.xaml`
- Modify: `src/QuickClipboard.App/Tray/TrayApplicationService.cs`

- [ ] **Step 1: Add favorite editor view model**

`FavoriteEditorViewModel` must expose:

```csharp
public string Title { get; set; }
public string Content { get; set; }
public string? Hotkey { get; set; }
public bool IsValid { get; }
public string? ValidationMessage { get; }
```

Validation requirements:

- title must not be blank;
- content must not be blank;
- hotkey may be blank;
- nonblank hotkey must satisfy `Hotkey.TryParse`.

- [ ] **Step 2: Add inline favorite editor UI**

In the `Favorites` tab:

- Add `New` button.
- Show editor fields for title, content, and hotkey when creating or editing.
- Add `Save` and `Cancel` buttons.
- Disable `Save` when `IsValid` is false.
- Keep editing inside the floating panel for MVP.

- [ ] **Step 3: Refresh favorite hotkeys**

In `TrayApplicationService`, add:

```csharp
private async Task RefreshFavoriteHotkeysAsync(CancellationToken cancellationToken = default)
```

Requirements:

- Unregister all hotkeys except the panel hotkey.
- Load favorites.
- For each favorite with nonblank hotkey, parse and register hotkey ID `favorite:{id}`.
- If registration fails, keep the favorite but write the failure to `Debug.WriteLine`.
- On `HotkeyPressed` with `favorite:{id}`, load that favorite and call `TextInsertionService.InsertTextAsync`.

- [ ] **Step 4: Call hotkey refresh after favorite changes**

After create, update, or delete favorite, call a callback from `FloatingPanelViewModel` to `TrayApplicationService.RefreshFavoriteHotkeysAsync`.

- [ ] **Step 5: Manual favorite test**

Run:

```powershell
dotnet run --project src/QuickClipboard.App/QuickClipboard.App.csproj
```

Test:

- Add a favorite from a history item.
- Add a favorite manually.
- Edit title/content.
- Assign `Ctrl+Alt+1`.
- Focus Notepad and press `Ctrl+Alt+1`.
- Confirm favorite content is inserted without opening the panel.
- Delete the favorite and confirm the hotkey no longer inserts text.

- [ ] **Step 6: Commit favorite editor and hotkeys**

Run:

```powershell
git add src/QuickClipboard.App
git commit -m "feat: add favorite editing and direct hotkeys"
```

## Task 16: Pause Recording, Clear History, And Settings Persistence Polish

**Files:**
- Modify: `src/QuickClipboard.App/Tray/TrayApplicationService.cs`
- Modify: `src/QuickClipboard.App/Presentation/FloatingPanelWindow.xaml`
- Modify: `src/QuickClipboard.App/Presentation/ViewModels/FloatingPanelViewModel.cs`

- [ ] **Step 1: Complete tray pause actions**

Implement tray menu actions:

- `Pause 10 minutes`: save settings with `PauseRecordingUntil = now + 10 minutes` and `PauseRecordingIndefinitely = false`.
- `Pause 1 hour`: save settings with `PauseRecordingUntil = now + 1 hour` and `PauseRecordingIndefinitely = false`.
- `Pause until resumed`: save settings with `PauseRecordingUntil = null` and `PauseRecordingIndefinitely = true`.
- `Resume Recording`: save settings with `PauseRecordingUntil = null` and `PauseRecordingIndefinitely = false`.

- [ ] **Step 2: Add clear history**

Wire tray `Clear History` and panel clear action to:

```csharp
await clipboardRepository.ClearClipboardItemsAsync(cancellationToken);
```

After clearing from the panel, clear `HistoryItems`.

- [ ] **Step 3: Add status text**

Panel should show:

- `Recording paused` when paused indefinitely or until a future timestamp.
- `No history yet` when history list is empty.
- `No favorites yet` when favorites list is empty.

- [ ] **Step 4: Manual settings test**

Run:

```powershell
dotnet run --project src/QuickClipboard.App/QuickClipboard.App.csproj
```

Test:

- Pause for 10 minutes, copy text, confirm history does not grow.
- Exit and restart, confirm paused state persists.
- Resume recording, copy text, confirm history grows.
- Clear history, exit and restart, confirm history stays empty.

- [ ] **Step 5: Commit pause and clear-history polish**

Run:

```powershell
git add src/QuickClipboard.App
git commit -m "feat: add recording controls"
```

## Task 17: Manual Test Checklist And Release Build

**Files:**
- Create: `docs/manual-test-checklist.md`
- Modify: `README.md`

- [ ] **Step 1: Add manual checklist**

Create `docs/manual-test-checklist.md`:

```markdown
# Quick Clipboard Manual Test Checklist

## Startup

- [ ] App starts without showing a main window.
- [ ] Tray icon appears.
- [ ] Tray Exit closes the process.

## Clipboard History

- [ ] Copy normal text from Notepad; it appears in history.
- [ ] Copy the same text twice; only one immediate history item appears.
- [ ] Copy `123456`; it is not recorded.
- [ ] Copy `password=my-secret-value`; it is not recorded.
- [ ] Clear history removes all history items.

## Floating Panel

- [ ] `Ctrl+Alt+V` opens the panel.
- [ ] Panel opens near caret in Notepad.
- [ ] Panel falls back to mouse position when caret is unavailable.
- [ ] Panel stays inside screen edges.
- [ ] `Esc` closes the panel.
- [ ] Clicking outside closes the panel.

## Insertion

- [ ] Clicking a history item inserts text into Notepad.
- [ ] Clicking a favorite item inserts text into Notepad.
- [ ] Previous clipboard content is restored after insertion.
- [ ] Unicode and Chinese text paste correctly.

## Favorites

- [ ] History item can be added to favorites.
- [ ] Favorite can be created manually.
- [ ] Favorite can be edited.
- [ ] Favorite can be deleted.
- [ ] Favorite hotkey inserts content without opening the panel.
- [ ] Deleted favorite hotkey no longer inserts content.

## Pause Recording

- [ ] Pause for 10 minutes blocks new history.
- [ ] Pause for 1 hour blocks new history.
- [ ] Pause until resumed blocks new history after restart.
- [ ] Resume recording allows new history.

## Compatibility

- [ ] Browser input field insertion works.
- [ ] VS Code editor insertion works.
- [ ] Common chat app insertion works.
- [ ] Behavior is acceptable with Chinese input method enabled.
- [ ] Elevated apps either work or fail without crashing.
- [ ] Multi-monitor positioning is acceptable.
```

- [ ] **Step 2: Update README**

Replace `README.md` with:

```markdown
# Quick Clipboard

Quick Clipboard is a Windows-native clipboard assistant. It runs in the system tray, records recent plain-text clipboard history locally, and opens a floating panel with `Ctrl+Alt+V`.

## MVP Features

- Plain-text clipboard history.
- Favorites for common snippets.
- Favorite global hotkeys.
- Floating history/favorites panel.
- Local SQLite storage.
- Sensitive-text filtering.
- Pause recording and clear history controls.

## Development

```powershell
dotnet restore QuickClipboard.sln
dotnet build QuickClipboard.sln
dotnet test QuickClipboard.sln
dotnet run --project src/QuickClipboard.App/QuickClipboard.App.csproj
```

Manual integration checks are listed in `docs/manual-test-checklist.md`.
```

- [ ] **Step 3: Run full verification**

Run:

```powershell
dotnet format QuickClipboard.sln --verify-no-changes
dotnet test QuickClipboard.sln
dotnet build QuickClipboard.sln -c Release
dotnet publish src/QuickClipboard.App/QuickClipboard.App.csproj -c Release -r win-x64 --self-contained false
```

Expected:

- format reports no changes;
- tests pass;
- release build succeeds;
- publish output is created under `src/QuickClipboard.App/bin/Release/net9.0-windows/win-x64/publish`.

- [ ] **Step 4: Complete manual checklist**

Run the app from:

```powershell
src\QuickClipboard.App\bin\Release\net9.0-windows\win-x64\publish\QuickClipboard.App.exe
```

Complete every item in `docs/manual-test-checklist.md`. If a compatibility item fails for a known limitation, record the app name and behavior in the final implementation notes.

- [ ] **Step 5: Commit docs and release readiness**

Run:

```powershell
git add README.md docs/manual-test-checklist.md
git commit -m "docs: add quick clipboard test checklist"
```

## Plan Self-Review

- Spec coverage: the tasks cover WPF app scaffolding, tray lifecycle, clipboard monitoring, text-only history, SQLite persistence, duplicate and sensitive filtering, panel hotkey, caret-first positioning with mouse fallback, history/favorites tabs, favorite CRUD, favorite global hotkeys, pause recording, clear history, insertion through temporary clipboard and `Ctrl+V`, and manual compatibility testing.
- Scope check: this is one cohesive MVP. Images, rich text, cloud sync, database encryption, plugin systems, and advanced animation remain outside the implementation tasks.
- Type consistency: shared names are consistent across tasks: `ClipboardItem`, `FavoriteItem`, `AppSettings`, `Hotkey`, `ClipboardCapturePolicy`, `SqliteClipboardRepository`, `SqliteSettingsRepository`, `ClipboardMonitor`, `GlobalHotkeyService`, `PanelPositionService`, `TextInsertionService`, `FloatingPanelViewModel`, and `TrayApplicationService`.
