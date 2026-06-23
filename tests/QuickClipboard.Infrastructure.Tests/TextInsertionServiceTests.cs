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
            new FakePasteShortcutSender(calls),
            TimeSpan.Zero);

        await service.InsertTextAsync("hello");

        calls.Should().Equal(
            "suppress",
            "get",
            "set-text:hello",
            "paste",
            "restore");
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

        public IDataObject? GetDataObject()
        {
            calls.Add("get");
            return original;
        }

        public void SetText(string text)
        {
            calls.Add($"set-text:{text}");
        }

        public void SetDataObject(IDataObject dataObject, bool copy)
        {
            dataObject.Should().BeSameAs(original);
            copy.Should().BeTrue();
            calls.Add("restore");
        }
    }

    private sealed class FakePasteShortcutSender(List<string> calls) : IPasteShortcutSender
    {
        public void SendPasteShortcut()
        {
            calls.Add("paste");
        }
    }
}
