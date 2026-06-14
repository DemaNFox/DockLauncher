using System.Windows.Controls;

namespace DockLauncher.AppHost.Docking;

public static class DockMagnificationBehavior
{
    public static double ComputeScale(double distanceFromCursor, double primaryRange = 110, double secondaryRange = 210)
    {
        var distance = Math.Abs(distanceFromCursor);

        if (distance <= primaryRange)
        {
            var progress = 1 - (distance / primaryRange);
            return 1.0 + (0.18 * progress);
        }

        if (distance <= secondaryRange)
        {
            var progress = 1 - ((distance - primaryRange) / (secondaryRange - primaryRange));
            return 1.0 + (0.05 * progress);
        }

        return 1.0;
    }

    public static double ComputeAxisDistance(Orientation orientation, double itemCenterX, double itemCenterY, double cursorX, double cursorY)
    {
        return orientation == Orientation.Horizontal
            ? itemCenterX - cursorX
            : itemCenterY - cursorY;
    }
}
