using System.Windows;
using FluentAssertions;
using QuickClipboard.App.Presentation;
using QuickClipboard.App.Presentation.ViewModels;

namespace QuickClipboard.App.Tests;

public sealed class FloatingPanelLauncherTests
{
    [Fact]
    public void OpenClosesExistingWindowBeforeShowingNewWindow()
    {
        var viewModels = new Queue<FloatingPanelViewModel?>([null, null]);
        var anchors = new Queue<Rect>([new Rect(10, 20, 1, 1), new Rect(30, 40, 1, 1)]);
        var windows = new List<FakeFloatingPanelWindow>();

        var launcher = new FloatingPanelLauncher(
            () => viewModels.Dequeue()!,
            new FakePanelAnchorProvider(() => anchors.Dequeue()),
            (viewModel, anchor) =>
            {
                var window = new FakeFloatingPanelWindow(anchor);
                windows.Add(window);
                return window;
            });

        launcher.Open();
        launcher.Open();

        windows.Should().HaveCount(2);
        windows[0].CloseCount.Should().Be(1);
        windows[0].ShowCount.Should().Be(1);
        windows[1].CloseCount.Should().Be(0);
        windows[1].ShowCount.Should().Be(1);
        windows[1].Anchor.Should().Be(new Rect(30, 40, 1, 1));
    }

    private sealed class FakePanelAnchorProvider(Func<Rect> getPreferredAnchor) : IPanelAnchorProvider
    {
        public Rect GetPreferredAnchor()
        {
            return getPreferredAnchor();
        }
    }

    private sealed class FakeFloatingPanelWindow(Rect anchor) : IFloatingPanelWindow
    {
        public int CloseCount { get; private set; }
        public int ShowCount { get; private set; }
        public Rect Anchor { get; } = anchor;

        public void Close()
        {
            CloseCount++;
        }

        public void Show()
        {
            ShowCount++;
        }
    }
}
