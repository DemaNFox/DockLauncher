using DockLauncher.BuildingBlocks.Domain.Abstractions;
using DockLauncher.BuildingBlocks.Domain.Guards;

namespace DockLauncher.Modules.Panels.Domain;

public sealed class Panel : AggregateRoot<Guid>
{
    private readonly List<Guid> _itemIds = [];

    public Panel(
        Guid id,
        string name,
        PanelPosition position,
        PanelLayoutMode layoutMode,
        PanelAppearance appearance)
        : base(id)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Position = position;
        LayoutMode = layoutMode;
        Appearance = appearance;
    }

    public string Name { get; private set; }

    public PanelPosition Position { get; private set; }

    public PanelLayoutMode LayoutMode { get; private set; }

    public PanelAppearance Appearance { get; private set; }

    public IReadOnlyCollection<Guid> ItemIds => _itemIds;

    public void Rename(string name)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
    }

    public void AddItem(Guid itemId)
    {
        if (!_itemIds.Contains(itemId))
        {
            _itemIds.Add(itemId);
        }
    }
}

public enum PanelPosition
{
    Top,
    Bottom,
    Left,
    Right,
    Floating
}

public enum PanelLayoutMode
{
    IconOnly,
    IconWithLabel,
    CompactList,
    Tiles,
    Grid
}

public enum PanelOrientation
{
    Horizontal,
    Vertical
}

public enum PanelCollapseButtonSide
{
    Left,
    Right,
    Top,
    Bottom
}

public enum PanelLabelDisplayMode
{
    AlwaysVisible,
    HoverOnly
}

public enum PanelLabelPlacement
{
    AboveIcon,
    BelowIcon
}

public enum PanelIconShape
{
    Circle,
    RoundedSquare,
    Square,
    Hexagon
}

public enum PanelFlyoutDisplayMode
{
    Tiles,
    List
}

public enum PanelGroupOpenMode
{
    Expand,
    Floating
}

public enum PanelOverflowMode
{
    Scroll,
    ExpandLayout
}

public sealed record PanelAppearance(
    double Opacity,
    int IconSize,
    bool AlwaysOnTop,
    bool LabelsVisible,
    bool AutoHide,
    bool Locked = false,
    double? FloatingLeft = null,
    double? FloatingTop = null,
    PanelLabelDisplayMode? LabelDisplayMode = null,
    PanelLabelPlacement? LabelPlacement = null,
    PanelIconShape? IconShape = null,
    double HorizontalPadding = 20d,
    double VerticalPadding = 18d,
    double LabelSpacing = 4d,
    double TextSize = 10.5d,
    string? PanelColor = null,
    PanelOrientation? Orientation = null,
    double? DockOffset = null,
    PanelFlyoutDisplayMode? FlyoutDisplayMode = null,
    PanelGroupOpenMode? GroupOpenMode = null,
    bool IsCollapsed = false,
    bool IsCollapsible = false,
    PanelCollapseButtonSide? CollapseButtonSide = null,
    bool AutoCollapse = false,
    int AutoCollapseDelaySeconds = 5,
    PanelOverflowMode? OverflowMode = null,
    int MaxOverflowTracks = 1,
    bool IsHidden = false,
    double? CustomWidth = null,
    double? CustomHeight = null)
{
    public PanelLabelDisplayMode ResolvedLabelDisplayMode => LabelDisplayMode ?? (LabelsVisible ? PanelLabelDisplayMode.AlwaysVisible : PanelLabelDisplayMode.HoverOnly);

    public PanelLabelPlacement ResolvedLabelPlacement => LabelPlacement ?? PanelLabelPlacement.BelowIcon;

    public PanelIconShape ResolvedIconShape => IconShape ?? PanelIconShape.Circle;

    public PanelFlyoutDisplayMode ResolvedFlyoutDisplayMode => FlyoutDisplayMode ?? PanelFlyoutDisplayMode.Tiles;

    public PanelGroupOpenMode ResolvedGroupOpenMode => GroupOpenMode ?? PanelGroupOpenMode.Floating;

    public PanelOverflowMode ResolvedOverflowMode => OverflowMode ?? PanelOverflowMode.Scroll;

    public int ResolvedMaxOverflowTracks => Math.Clamp(MaxOverflowTracks, 1, 6);

    public string ResolvedPanelColor => string.IsNullOrWhiteSpace(PanelColor) ? "#1B2637" : PanelColor;
}
