using System.Windows;
using QuickClipboard.Infrastructure.Windows;

namespace QuickClipboard.App.Presentation;

public sealed class PanelAnchorProvider(PanelPositionService panelPositionService) : IPanelAnchorProvider
{
    public Rect GetPreferredAnchor()
    {
        return panelPositionService.GetPreferredAnchor();
    }
}
