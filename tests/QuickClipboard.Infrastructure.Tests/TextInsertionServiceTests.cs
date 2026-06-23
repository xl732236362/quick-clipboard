using System.Runtime.InteropServices;
using FluentAssertions;
using QuickClipboard.Core.Services;
using QuickClipboard.Infrastructure.Windows;
using DataObject = System.Windows.DataObject;
using IDataObject = System.Windows.IDataObject;

namespace QuickClipboard.Infrastructure.Tests;

public sealed class TextInsertionServiceTests
{
    [Fact]
    public async Task InsertTextSuppressesClipboardRecordingBeforeSettingAndRestoringClipboard()
    {
        var calls = new List<string>();
        var suppressor = new FakeClipboardChangeSuppressor(calls);
        var clipboard = new FakeClipboardGateway(calls);
        var service = new TextInsertionService(
            suppressor,
            clipboard,
            new FakeClipboardTextWriter(calls),
            new FakePasteShortcutSender(calls),
            textKeyboard: null,
            clipboardRestoreDelay: TimeSpan.Zero);

        await service.InsertTextAsync("hello");

        calls.Should().Equal(
            "suppress",
            "get",
            "set-text:hello",
            "paste",
            "restore");
    }

    [Fact]
    public async Task InsertTextRetriesWhenClipboardIsTemporarilyUnavailable()
    {
        var calls = new List<string>();
        var suppressor = new FakeClipboardChangeSuppressor(calls);
        var clipboard = new FakeClipboardGateway(calls)
        {
            SetTextFailuresBeforeSuccess = 1
        };
        var service = new TextInsertionService(
            suppressor,
            clipboard,
            new FakeClipboardTextWriter(calls)
            {
                FailuresBeforeSuccess = 1
            },
            new FakePasteShortcutSender(calls),
            textKeyboard: null,
            clipboardRestoreDelay: TimeSpan.Zero,
            clipboardRetryCount: 2,
            clipboardRetryDelay: TimeSpan.Zero);

        await service.InsertTextAsync("hello");

        calls.Should().Equal(
            "suppress",
            "get",
            "set-text:hello",
            "suppress",
            "get",
            "set-text:hello",
            "paste",
            "restore");
    }

    [Fact]
    public async Task InsertTextFallsBackToUnicodeInputWhenClipboardRemainsUnavailable()
    {
        var calls = new List<string>();
        var suppressor = new FakeClipboardChangeSuppressor(calls);
        var clipboard = new FakeClipboardGateway(calls)
        {
            SetTextFailuresBeforeSuccess = 5
        };
        var keyboard = new FakeTextKeyboard(calls);
        var service = new TextInsertionService(
            suppressor,
            clipboard,
            new FakeClipboardTextWriter(calls)
            {
                FailuresBeforeSuccess = 5
            },
            new FakePasteShortcutSender(calls),
            textKeyboard: keyboard,
            clipboardRestoreDelay: TimeSpan.Zero,
            clipboardRetryCount: 2,
            clipboardRetryDelay: TimeSpan.Zero);

        await service.InsertTextAsync("\u4F60\u597D");

        calls.Should().ContainInOrder(
            "set-text:\u4F60\u597D",
            "set-text:\u4F60\u597D",
            "type:\u4F60\u597D");
        calls.Should().NotContain("paste");
        calls.Should().NotContain("restore");
    }

    [Fact]
    public async Task InsertTextRecordsClipboardAndFocusDiagnosticsBeforeUnicodeFallback()
    {
        var calls = new List<string>();
        var suppressor = new FakeClipboardChangeSuppressor(calls);
        var clipboard = new FakeClipboardGateway(calls)
        {
            SetTextFailuresBeforeSuccess = 5
        };
        var keyboard = new FakeTextKeyboard(calls);
        var diagnostics = new FakeTextInsertionDiagnostics(calls);
        var service = new TextInsertionService(
            suppressor,
            clipboard,
            new FakeClipboardTextWriter(calls)
            {
                FailuresBeforeSuccess = 5
            },
            new FakePasteShortcutSender(calls),
            textKeyboard: keyboard,
            clipboardRestoreDelay: TimeSpan.Zero,
            clipboardRetryCount: 2,
            clipboardRetryDelay: TimeSpan.Zero,
            diagnostics: diagnostics);

        await service.InsertTextAsync("hello");

        calls.Should().ContainInOrder(
            "clipboard-diagnostics",
            "focus-diagnostics",
            "type:hello",
            "focus-diagnostics");
    }

    private sealed class FakeClipboardChangeSuppressor(List<string> calls) : IClipboardChangeSuppressor
    {
        public void SuppressNextChanges()
        {
            calls.Add("suppress");
        }
    }

    private sealed class FakeClipboardGateway(List<string> calls) : IClipboardGateway
    {
        private readonly IDataObject original = new DataObject("original");
        public int SetTextFailuresBeforeSuccess { get; init; }

        public IDataObject? GetDataObject()
        {
            calls.Add("get");
            return original;
        }

        public void SetDataObject(IDataObject dataObject, bool copy)
        {
            dataObject.Should().BeSameAs(original);
            copy.Should().BeTrue();
            calls.Add("restore");
        }
    }

    private sealed class FakeClipboardTextWriter(List<string> calls) : IClipboardTextWriter
    {
        private int setTextCallCount;

        public int FailuresBeforeSuccess { get; init; }

        public void SetUnicodeText(string text)
        {
            calls.Add($"set-text:{text}");
            setTextCallCount++;
            if (setTextCallCount <= FailuresBeforeSuccess)
            {
                throw new ExternalException("clipboard unavailable");
            }
        }
    }

    private sealed class FakePasteShortcutSender(List<string> calls) : IPasteShortcutSender
    {
        public void SendPasteShortcut()
        {
            calls.Add("paste");
        }
    }

    private sealed class FakeTextKeyboard(List<string> calls) : ITextKeyboard
    {
        public void TypeText(string text)
        {
            calls.Add($"type:{text}");
        }
    }

    private sealed class FakeTextInsertionDiagnostics(List<string> calls) : ITextInsertionDiagnostics
    {
        public string DescribeClipboardOwner()
        {
            calls.Add("clipboard-diagnostics");
            return "clipboard-owner=fake";
        }

        public string DescribeCurrentFocus()
        {
            calls.Add("focus-diagnostics");
            return "focus=fake";
        }
    }
}
