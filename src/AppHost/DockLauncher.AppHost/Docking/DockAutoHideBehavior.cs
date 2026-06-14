using System.Windows;
using DockLauncher.Modules.Panels.Domain;

namespace DockLauncher.AppHost.Docking;

public static class DockAutoHideBehavior
{
    public static double GetCollapsedLeft(PanelPosition position, Rect workArea, Rect currentBounds, double visibleEdge)
    {
        return position switch
        {
            PanelPosition.Left => workArea.Left - currentBounds.Width + visibleEdge,
            PanelPosition.Right => workArea.Right - visibleEdge,
            _ => currentBounds.Left
        };
    }

    public static double GetCollapsedTop(PanelPosition position, Rect workArea, Rect currentBounds, double visibleEdge)
    {
        return position switch
        {
            PanelPosition.Top => workArea.Top - currentBounds.Height + visibleEdge,
            PanelPosition.Bottom => workArea.Bottom - visibleEdge,
            _ => currentBounds.Top
        };
    }

    public static double GetCollapsedTop(PanelPosition position, Rect workArea, Rect screenArea, Rect currentBounds, double visibleEdge, bool collapseBehindSystemTaskbar)
    {
        if (!collapseBehindSystemTaskbar)
        {
            return GetCollapsedTop(position, workArea, currentBounds, visibleEdge);
        }

        return position switch
        {
            PanelPosition.Top => screenArea.Top - currentBounds.Height + visibleEdge,
            PanelPosition.Bottom => screenArea.Bottom - visibleEdge,
            _ => currentBounds.Top
        };
    }

    public static bool ShouldReveal(PanelPosition position, Rect workArea, Rect currentBounds, Point cursor, double proximity)
    {
        if (currentBounds.Contains(cursor))
        {
            return true;
        }

        return position switch
        {
            PanelPosition.Left => cursor.X <= workArea.Left + proximity
                && IsWithin(cursor.Y, currentBounds.Top, currentBounds.Bottom, proximity),
            PanelPosition.Right => cursor.X >= workArea.Right - proximity
                && IsWithin(cursor.Y, currentBounds.Top, currentBounds.Bottom, proximity),
            PanelPosition.Top => cursor.Y <= workArea.Top + proximity
                && IsWithin(cursor.X, currentBounds.Left, currentBounds.Right, proximity),
            PanelPosition.Bottom => cursor.Y >= workArea.Bottom - proximity
                && IsWithin(cursor.X, currentBounds.Left, currentBounds.Right, proximity),
            _ => false
        };
    }

    private static bool IsWithin(double value, double start, double end, double tolerance)
    {
        return value >= start - tolerance && value <= end + tolerance;
    }
}
