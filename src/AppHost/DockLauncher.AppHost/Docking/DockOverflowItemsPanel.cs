using System.Windows;
using System.Windows.Controls;

namespace DockLauncher.AppHost.Docking;

public sealed class DockOverflowItemsPanel : Panel
{
    public static readonly DependencyProperty ItemsOrientationProperty = DependencyProperty.Register(
        nameof(ItemsOrientation),
        typeof(Orientation),
        typeof(DockOverflowItemsPanel),
        new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty TrackCountProperty = DependencyProperty.Register(
        nameof(TrackCount),
        typeof(int),
        typeof(DockOverflowItemsPanel),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty PrimarySlotCountProperty = DependencyProperty.Register(
        nameof(PrimarySlotCount),
        typeof(int),
        typeof(DockOverflowItemsPanel),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemExtentWidthProperty = DependencyProperty.Register(
        nameof(ItemExtentWidth),
        typeof(double),
        typeof(DockOverflowItemsPanel),
        new FrameworkPropertyMetadata(80d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemExtentHeightProperty = DependencyProperty.Register(
        nameof(ItemExtentHeight),
        typeof(double),
        typeof(DockOverflowItemsPanel),
        new FrameworkPropertyMetadata(80d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public Orientation ItemsOrientation
    {
        get => (Orientation)GetValue(ItemsOrientationProperty);
        set => SetValue(ItemsOrientationProperty, value);
    }

    public int TrackCount
    {
        get => (int)GetValue(TrackCountProperty);
        set => SetValue(TrackCountProperty, value);
    }

    public int PrimarySlotCount
    {
        get => (int)GetValue(PrimarySlotCountProperty);
        set => SetValue(PrimarySlotCountProperty, value);
    }

    public double ItemExtentWidth
    {
        get => (double)GetValue(ItemExtentWidthProperty);
        set => SetValue(ItemExtentWidthProperty, value);
    }

    public double ItemExtentHeight
    {
        get => (double)GetValue(ItemExtentHeightProperty);
        set => SetValue(ItemExtentHeightProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var cellSize = new Size(ItemExtentWidth, ItemExtentHeight);
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(cellSize);
        }

        var primarySlots = GetPrimarySlotCount();
        return ItemsOrientation == Orientation.Horizontal
            ? new Size(primarySlots * ItemExtentWidth, TrackCount * ItemExtentHeight)
            : new Size(TrackCount * ItemExtentWidth, primarySlots * ItemExtentHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var primarySlots = GetPrimarySlotCount();
        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var column = ItemsOrientation == Orientation.Horizontal
                ? index % primarySlots
                : index / primarySlots;
            var row = ItemsOrientation == Orientation.Horizontal
                ? index / primarySlots
                : index % primarySlots;

            InternalChildren[index].Arrange(new Rect(
                column * ItemExtentWidth,
                row * ItemExtentHeight,
                ItemExtentWidth,
                ItemExtentHeight));
        }

        return finalSize;
    }

    private int GetPrimarySlotCount()
    {
        if (PrimarySlotCount > 0)
        {
            return PrimarySlotCount;
        }

        return Math.Max(1, (int)Math.Ceiling(InternalChildren.Count / (double)Math.Max(1, TrackCount)));
    }
}
