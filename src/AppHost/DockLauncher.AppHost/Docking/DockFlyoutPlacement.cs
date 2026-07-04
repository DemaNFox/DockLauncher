using System.Windows;
using DockLauncher.Modules.Panels.Domain;

namespace DockLauncher.AppHost.Docking;

public static class DockFlyoutPlacement
{
    public static Point Calculate(PanelPosition panelPosition, Rect panelBounds, Size flyoutSize, Rect workArea)
    {
        const double gap = 14;
        const double margin = 12;

        var left = panelBounds.Left;
        var top = panelBounds.Top;

        switch (panelPosition)
        {
            case PanelPosition.Top:
                left = panelBounds.Left + (panelBounds.Width - flyoutSize.Width) / 2;
                top = panelBounds.Bottom + gap;
                break;
            case PanelPosition.Bottom:
                left = panelBounds.Left + (panelBounds.Width - flyoutSize.Width) / 2;
                top = panelBounds.Top - flyoutSize.Height - gap;
                break;
            case PanelPosition.Left:
                left = panelBounds.Right + gap;
                top = panelBounds.Top + (panelBounds.Height - flyoutSize.Height) / 2;
                break;
            case PanelPosition.Right:
                left = panelBounds.Left - flyoutSize.Width - gap;
                top = panelBounds.Top + (panelBounds.Height - flyoutSize.Height) / 2;
                break;
            case PanelPosition.Floating:
            default:
                var availableBelow = workArea.Bottom - margin - panelBounds.Bottom;
                var availableAbove = panelBounds.Top - (workArea.Top + margin);
                var availableRight = workArea.Right - margin - panelBounds.Right;
                var availableLeft = panelBounds.Left - (workArea.Left + margin);

                if (availableBelow >= flyoutSize.Height + gap)
                {
                    left = panelBounds.Left + (panelBounds.Width - flyoutSize.Width) / 2;
                    top = panelBounds.Bottom + gap;
                }
                else if (availableAbove >= flyoutSize.Height + gap)
                {
                    left = panelBounds.Left + (panelBounds.Width - flyoutSize.Width) / 2;
                    top = panelBounds.Top - flyoutSize.Height - gap;
                }
                else if (availableRight >= flyoutSize.Width + gap)
                {
                    left = panelBounds.Right + gap;
                    top = panelBounds.Top + (panelBounds.Height - flyoutSize.Height) / 2;
                }
                else if (availableLeft >= flyoutSize.Width + gap)
                {
                    left = panelBounds.Left - flyoutSize.Width - gap;
                    top = panelBounds.Top + (panelBounds.Height - flyoutSize.Height) / 2;
                }
                else if (Math.Max(availableBelow, availableAbove) >= Math.Max(availableRight, availableLeft))
                {
                    left = panelBounds.Left + (panelBounds.Width - flyoutSize.Width) / 2;
                    top = availableBelow >= availableAbove
                        ? panelBounds.Bottom + gap
                        : panelBounds.Top - flyoutSize.Height - gap;
                }
                else
                {
                    left = availableRight >= availableLeft
                        ? panelBounds.Right + gap
                        : panelBounds.Left - flyoutSize.Width - gap;
                    top = panelBounds.Top + (panelBounds.Height - flyoutSize.Height) / 2;
                }

                break;
        }

        left = Math.Clamp(left, workArea.Left + margin, workArea.Right - flyoutSize.Width - margin);
        top = Math.Clamp(top, workArea.Top + margin, workArea.Bottom - flyoutSize.Height - margin);
        return new Point(left, top);
    }
}
