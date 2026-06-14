using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLauncher.BuildingBlocks.Presentation.Wpf;
using DockLauncher.Modules.Items.Domain;
using DockLauncher.Modules.Panels.Domain;

namespace DockLauncher.AppHost.Docking;

public sealed partial class DockPanelWindowViewModel : ViewModelBase
{
    private const double OverflowIndicatorGutter = 24d;

    private readonly Func<DockPanelItemViewModel, Task> _activateItemAsync;
    private readonly Action _showConfigurator;
    private readonly Func<Task> _refreshPanelsAsync;
    private readonly Func<IReadOnlyList<string>, Task> _addDroppedPathsAsync;
    private readonly Func<Task> _addFileAsync;
    private readonly Func<Task> _addFolderAsync;
    private readonly Func<Task> _addSeparatorAsync;
    private readonly Func<Task> _importPinnedShortcutsAsync;
    private readonly Func<Task> _openGroupsEditorAsync;
    private readonly Func<Task> _openLaunchProfilesEditorAsync;
    private readonly Func<PanelPosition, double?, double?, double?, double?, Task> _updatePanelPositionAsync;
    private readonly Func<Task> _renamePanelAsync;
    private readonly Func<Task> _duplicatePanelAsync;
    private readonly Func<Task> _createEmptyPanelAsync;
    private readonly Func<Task> _removePanelAsync;
    private readonly Func<Task> _hidePanelAsync;
    private readonly Func<Task> _togglePanelLockAsync;
    private readonly Func<Task> _toggleLabelsAsync;
    private readonly Func<Task> _toggleAlwaysOnTopAsync;
    private readonly Func<Task> _toggleAutoHideAsync;
    private readonly Func<PanelOrientation, Task> _setPanelOrientationAsync;
    private readonly Func<Task> _increaseIconSizeAsync;
    private readonly Func<Task> _decreaseIconSizeAsync;
    private readonly Func<DockPanelItemViewModel, Task> _openLocationAsync;
    private readonly Func<DockPanelItemViewModel, Task> _duplicateAsync;
    private readonly Func<DockPanelItemViewModel, Task> _duplicateToNewPanelAsync;
    private readonly Func<DockPanelItemViewModel, Task> _removeAsync;
    private readonly Func<DockPanelItemViewModel, Task> _renameAsync;
    private readonly Func<DockPanelItemViewModel, Task> _editAsync;
    private readonly Func<DockPanelItemViewModel, DockPanelMoveTargetViewModel, Task> _moveToPanelAsync;
    private readonly Func<Guid, Guid?, Task> _moveWithinPanelAsync;
    private readonly Func<bool> _hasOpenFlyouts;
    private readonly Action _exit;

    public DockPanelWindowViewModel(
        Guid panelId,
        string title,
        PanelPosition position,
        Orientation itemsOrientation,
        double windowWidth,
        double windowHeight,
        double expandedWindowWidth,
        double expandedWindowHeight,
        double collapsedWindowWidth,
        double collapsedWindowHeight,
        double left,
        double top,
        bool horizontalScrollEnabled,
        bool verticalScrollEnabled,
        bool isTopmost,
        double opacity,
        int itemSize,
        double horizontalPadding,
        double verticalPadding,
        double labelSpacing,
        double textSize,
        string panelColor,
        PanelLabelDisplayMode labelDisplayMode,
        PanelLabelPlacement labelPlacement,
        PanelIconShape iconShape,
        PanelOverflowMode overflowMode,
        int maxOverflowTracks,
        int activeOverflowTracks,
        int primaryVisibleSlots,
        bool overflowActive,
        bool autoHide,
        bool isLocked,
        IReadOnlyList<DockPanelItemViewModel> items,
        Func<DockPanelItemViewModel, Task> activateItemAsync,
        Action showConfigurator,
        Func<Task> refreshPanelsAsync,
        Func<IReadOnlyList<string>, Task> addDroppedPathsAsync,
        Func<Task> addFileAsync,
        Func<Task> addFolderAsync,
        Func<Task> addSeparatorAsync,
        Func<Task> importPinnedShortcutsAsync,
        Func<Task> openGroupsEditorAsync,
        Func<Task> openLaunchProfilesEditorAsync,
        Func<PanelPosition, double?, double?, double?, double?, Task> updatePanelPositionAsync,
        Func<Task> renamePanelAsync,
        Func<Task> duplicatePanelAsync,
        Func<Task> createEmptyPanelAsync,
        Func<Task> removePanelAsync,
        Func<Task> hidePanelAsync,
        Func<Task> togglePanelLockAsync,
        Func<Task> toggleLabelsAsync,
        Func<Task> toggleAlwaysOnTopAsync,
        Func<Task> toggleAutoHideAsync,
        Func<PanelOrientation, Task> setPanelOrientationAsync,
        Func<Task> increaseIconSizeAsync,
        Func<Task> decreaseIconSizeAsync,
        Func<DockPanelItemViewModel, Task> openLocationAsync,
        Func<DockPanelItemViewModel, Task> duplicateAsync,
        Func<DockPanelItemViewModel, Task> duplicateToNewPanelAsync,
        Func<DockPanelItemViewModel, Task> removeAsync,
        Func<DockPanelItemViewModel, Task> renameAsync,
        Func<DockPanelItemViewModel, Task> editAsync,
        Func<DockPanelItemViewModel, DockPanelMoveTargetViewModel, Task> moveToPanelAsync,
        Func<Guid, Guid?, Task> moveWithinPanelAsync,
        Func<bool> hasOpenFlyouts,
        Action exit)
    {
        PanelId = panelId;
        Title = title;
        Position = position;
        ItemsOrientation = itemsOrientation;
        WindowWidth = windowWidth;
        WindowHeight = windowHeight;
        ExpandedWindowWidth = expandedWindowWidth;
        ExpandedWindowHeight = expandedWindowHeight;
        CollapsedWindowWidth = collapsedWindowWidth;
        CollapsedWindowHeight = collapsedWindowHeight;
        Left = left;
        Top = top;
        HorizontalScrollEnabled = horizontalScrollEnabled;
        VerticalScrollEnabled = verticalScrollEnabled;
        IsTopmost = isTopmost;
        Opacity = opacity;
        ItemSize = itemSize;
        HorizontalPadding = horizontalPadding;
        VerticalPadding = verticalPadding;
        LabelSpacing = labelSpacing;
        TextSize = textSize;
        PanelColor = panelColor;
        LabelDisplayMode = labelDisplayMode;
        LabelPlacement = labelPlacement;
        IconShape = iconShape;
        OverflowMode = overflowMode;
        MaxOverflowTracks = Math.Clamp(maxOverflowTracks, 1, 6);
        ActiveOverflowTracks = overflowMode == PanelOverflowMode.Scroll
            ? 1
            : Math.Clamp(activeOverflowTracks, 1, 6);
        PrimaryVisibleSlots = Math.Max(1, primaryVisibleSlots);
        OverflowActive = overflowActive;
        AutoHideEnabled = autoHide;
        IsLocked = isLocked;
        Items = items;
        _activateItemAsync = activateItemAsync;
        _showConfigurator = showConfigurator;
        _refreshPanelsAsync = refreshPanelsAsync;
        _addDroppedPathsAsync = addDroppedPathsAsync;
        _addFileAsync = addFileAsync;
        _addFolderAsync = addFolderAsync;
        _addSeparatorAsync = addSeparatorAsync;
        _importPinnedShortcutsAsync = importPinnedShortcutsAsync;
        _openGroupsEditorAsync = openGroupsEditorAsync;
        _openLaunchProfilesEditorAsync = openLaunchProfilesEditorAsync;
        _updatePanelPositionAsync = updatePanelPositionAsync;
        _renamePanelAsync = renamePanelAsync;
        _duplicatePanelAsync = duplicatePanelAsync;
        _createEmptyPanelAsync = createEmptyPanelAsync;
        _removePanelAsync = removePanelAsync;
        _hidePanelAsync = hidePanelAsync;
        _togglePanelLockAsync = togglePanelLockAsync;
        _toggleLabelsAsync = toggleLabelsAsync;
        _toggleAlwaysOnTopAsync = toggleAlwaysOnTopAsync;
        _toggleAutoHideAsync = toggleAutoHideAsync;
        _setPanelOrientationAsync = setPanelOrientationAsync;
        _increaseIconSizeAsync = increaseIconSizeAsync;
        _decreaseIconSizeAsync = decreaseIconSizeAsync;
        _openLocationAsync = openLocationAsync;
        _duplicateAsync = duplicateAsync;
        _duplicateToNewPanelAsync = duplicateToNewPanelAsync;
        _removeAsync = removeAsync;
        _renameAsync = renameAsync;
        _editAsync = editAsync;
        _moveToPanelAsync = moveToPanelAsync;
        _moveWithinPanelAsync = moveWithinPanelAsync;
        _hasOpenFlyouts = hasOpenFlyouts;
        _exit = exit;

        foreach (var item in Items)
        {
            item.AttachLauncher(LaunchSelectedItemAsync);
            item.AttachContextActions(
                LaunchSelectedItemAsAdministratorAsync,
                OpenSelectedItemLocationAsync,
                DuplicateSelectedItemAsync,
                DuplicateSelectedItemToNewPanelAsync,
                RemoveSelectedItemAsync,
                RenameSelectedItemAsync,
                EditSelectedItemAsync,
                MoveSelectedItemToPanelAsync);
        }
    }

    public Guid PanelId { get; }

    public string Title { get; }

    public PanelPosition Position { get; }

    public Orientation ItemsOrientation { get; }

    public bool IsHorizontalOrientation => ItemsOrientation == Orientation.Horizontal;

    public bool IsVerticalOrientation => ItemsOrientation == Orientation.Vertical;

    public double WindowWidth { get; }

    public double WindowHeight { get; }

    public double ExpandedWindowWidth { get; }

    public double ExpandedWindowHeight { get; }

    public double CollapsedWindowWidth { get; }

    public double CollapsedWindowHeight { get; }

    public double PanelBodyWidth => WindowWidth;

    public double PanelBodyHeight => WindowHeight;

    public double Left { get; }

    public double Top { get; }

    public bool HorizontalScrollEnabled { get; }

    public bool VerticalScrollEnabled { get; }

    public bool IsTopmost { get; }

    public double Opacity { get; }

    public int ItemSize { get; }

    public double HorizontalPadding { get; }

    public double VerticalPadding { get; }

    public double LabelSpacing { get; }

    public double TextSize { get; }

    public string PanelColor { get; }

    public Thickness PanelPadding => new(HorizontalPadding, VerticalPadding, HorizontalPadding, VerticalPadding);

    public Thickness EffectivePanelPadding => IsHorizontalOrientation
        ? new Thickness(HorizontalEdgePadding, VerticalPadding, HorizontalEdgePadding, VerticalPadding)
        : new Thickness(HorizontalPadding, VerticalEdgePadding, HorizontalPadding, VerticalEdgePadding);

    public Thickness DockItemMargin => IsHorizontalOrientation
        ? new Thickness(HorizontalItemGap / 2, 0, HorizontalItemGap / 2, 0)
        : new Thickness(0, VerticalItemGap / 2, 0, VerticalItemGap / 2);

    public double HorizontalItemGap => ResolveItemGap(HorizontalPadding);

    public double VerticalItemGap => ResolveItemGap(VerticalPadding);

    public double HorizontalEdgePadding => FixedEdgePadding;

    public double VerticalEdgePadding => FixedEdgePadding;

    public Thickness TopLabelMargin => new(0, 0, 0, LabelSpacing);

    public Thickness BottomLabelMargin => new(0, LabelSpacing, 0, 0);

    public double HoverHeadroom => Math.Ceiling(Math.Clamp(ItemSize * 0.09, 4, 8));

    public double ItemSlotWidth => LabelsVisible
        ? Math.Max(ItemSize + 30, 88)
        : Math.Max(ItemSize + 18, 56);

    public double LabelTextWidth => Math.Max(48, ItemSlotWidth - 10);

    public double ItemSlotHeight => ItemSize + HoverHeadroom;

    public double ItemVisualHeight => ItemSlotHeight + VisibleLabelHeight;

    public double IconChromeSize => Math.Clamp(ItemSize * 0.82, 28, 92);

    public double IconBackplateSize => Math.Clamp(IconChromeSize - 3, 24, 88);

    public double IconArtworkSize => Math.Clamp(IconChromeSize - 8, 20, 82);

    public double IconGlyphSize => Math.Clamp(IconBackplateSize * 0.62, 18, 42);

    public double SeparatorLineWidth => IsHorizontalOrientation ? 2 : IconChromeSize;

    public double SeparatorLineHeight => IsHorizontalOrientation ? IconChromeSize : 2;

    public double SeparatorSlotWidth => IsHorizontalOrientation ? 18 : ItemSlotWidth;

    public double SeparatorSlotHeight => IsHorizontalOrientation ? ItemSlotHeight : 18;

    public double LabelMinHeight => Math.Ceiling(TextSize + 7);

    public double VisibleLabelHeight => LabelsVisible
        ? LabelSpacing + LabelMinHeight
        : 0d;

    private static double ResolveItemGap(double padding)
    {
        return Math.Clamp(Math.Round(padding * 0.64), 0, 30);
    }

    private const double FixedEdgePadding = 12d;

    public PanelLabelDisplayMode LabelDisplayMode { get; }

    public PanelLabelPlacement LabelPlacement { get; }

    public PanelIconShape IconShape { get; }

    public PanelOverflowMode OverflowMode { get; }

    public int MaxOverflowTracks { get; }

    public int ActiveOverflowTracks { get; }

    public int PrimaryVisibleSlots { get; }

    public bool OverflowActive { get; }

    public double OverflowStartOffset => OverflowActive ? OverflowIndicatorGutter : 0d;

    public double OverflowPrimaryGutter => OverflowStartOffset * 2;

    public Thickness OverflowScrollViewerMargin => IsHorizontalOrientation
        ? new Thickness(OverflowStartOffset, 0, OverflowStartOffset, 0)
        : new Thickness(0, OverflowStartOffset, 0, OverflowStartOffset);

    public HorizontalAlignment OverflowItemsHorizontalAlignment => IsHorizontalOrientation
        ? HorizontalAlignment.Left
        : HorizontalAlignment.Center;

    public VerticalAlignment OverflowItemsVerticalAlignment => IsHorizontalOrientation
        ? VerticalAlignment.Center
        : VerticalAlignment.Top;

    public double ItemExtent => IsHorizontalOrientation
        ? ItemSlotWidth + HorizontalItemGap
        : ItemVisualHeight + VerticalItemGap;

    public double CrossTrackExtent => IsHorizontalOrientation
        ? ItemVisualHeight + VerticalItemGap
        : ItemSlotWidth + HorizontalItemGap;

    public int WrapPanelPrimaryConstraint => PrimaryVisibleSlots;

    public int WrapPanelCrossConstraint => ActiveOverflowTracks;

    public int TotalPrimarySlots => Math.Max(1, (int)Math.Ceiling(Items.Count / (double)Math.Max(1, ActiveOverflowTracks)));

    public int MaxOverflowScrollIndex => OverflowActive
        ? Math.Max(0, TotalPrimarySlots - PrimaryVisibleSlots)
        : 0;

    public double OverflowViewportWidth => IsHorizontalOrientation
        ? PrimaryVisibleSlots * ItemExtent
        : ActiveOverflowTracks * CrossTrackExtent;

    public double OverflowViewportHeight => IsHorizontalOrientation
        ? ActiveOverflowTracks * CrossTrackExtent
        : PrimaryVisibleSlots * ItemExtent;

    public double ItemExtentWidth => IsHorizontalOrientation ? ItemExtent : CrossTrackExtent;

    public double ItemExtentHeight => IsHorizontalOrientation ? CrossTrackExtent : ItemExtent;

    public Visibility HorizontalOverflowIndicatorVisibility => OverflowActive && IsHorizontalOrientation ? Visibility.Visible : Visibility.Collapsed;

    public Visibility VerticalOverflowIndicatorVisibility => OverflowActive && IsVerticalOrientation ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CircleShapeVisibility => IconShape == PanelIconShape.Circle ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RoundedSquareShapeVisibility => IconShape == PanelIconShape.RoundedSquare ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SquareShapeVisibility => IconShape == PanelIconShape.Square ? Visibility.Visible : Visibility.Collapsed;

    public Visibility HexagonShapeVisibility => IconShape == PanelIconShape.Hexagon ? Visibility.Visible : Visibility.Collapsed;

    public CornerRadius RoundedSquareCornerRadius => new(Math.Max(12, Math.Round(IconChromeSize * 0.24)));

    public CornerRadius IconBackplateCircleCornerRadius => new(Math.Round(IconBackplateSize / 2, MidpointRounding.AwayFromZero));

    public CornerRadius IconBackplateRoundedSquareCornerRadius => new(Math.Max(8, Math.Round(IconBackplateSize * 0.24)));

    public Visibility TopLabelsVisibility => LabelDisplayMode == PanelLabelDisplayMode.AlwaysVisible && LabelPlacement == PanelLabelPlacement.AboveIcon
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility BottomLabelsVisibility => LabelDisplayMode == PanelLabelDisplayMode.AlwaysVisible && LabelPlacement == PanelLabelPlacement.BelowIcon
        ? Visibility.Visible
        : Visibility.Collapsed;

    public bool HoverLabelsEnabled => LabelDisplayMode == PanelLabelDisplayMode.HoverOnly;

    public Brush PanelBackgroundBrush => CreatePanelBackgroundBrush();

    public Brush PanelBorderBrush => CreatePanelBorderBrush();

    public Brush IconHoverBackgroundBrush => CreateIconHoverBackgroundBrush();

    public Brush IconHoverBorderBrush => CreateIconHoverBorderBrush();

    public ScrollBarVisibility HorizontalItemsScrollBarVisibility => HorizontalScrollEnabled
        ? ScrollBarVisibility.Auto
        : ScrollBarVisibility.Disabled;

    public ScrollBarVisibility VerticalItemsScrollBarVisibility => VerticalScrollEnabled
        ? ScrollBarVisibility.Auto
        : ScrollBarVisibility.Disabled;

    public PlacementMode HoverLabelPlacement => ResolveHoverLabelPlacement();

    public double HoverLabelVerticalOffset => HoverLabelPlacement == PlacementMode.Top ? -10 : 10;

    public Point DockItemTransformOrigin => ResolveDockItemTransformOrigin();

    public VerticalAlignment ItemButtonVerticalAlignment => ResolveItemButtonVerticalAlignment();

    public bool AutoHideEnabled { get; }

    public bool CanAutoHide => Position != PanelPosition.Floating;

    public string AutoHideTooltip => CanAutoHide
        ? "Hide this panel until the cursor reaches its screen edge."
        : "Auto Hide is available only for panels docked to a screen edge.";

    public bool LabelsVisible => LabelDisplayMode == PanelLabelDisplayMode.AlwaysVisible;

    public bool IsLocked { get; }

    public IReadOnlyList<DockPanelItemViewModel> Items { get; }

    public bool HasOpenFlyouts => _hasOpenFlyouts();

    public Task AddDroppedPathsAsync(IReadOnlyList<string> paths)
    {
        return _addDroppedPathsAsync(paths);
    }

    public Task MoveItemWithinPanelAsync(Guid itemId, Guid? targetItemId)
    {
        return _moveWithinPanelAsync(itemId, targetItemId);
    }

    public Task UpdatePanelPositionAsync(
        PanelPosition position,
        double? floatingLeft = null,
        double? floatingTop = null,
        double? customWidth = null,
        double? customHeight = null)
    {
        return _updatePanelPositionAsync(position, floatingLeft, floatingTop, customWidth, customHeight);
    }

    [RelayCommand]
    private void ShowConfigurator()
    {
        _showConfigurator();
    }

    [RelayCommand]
    private Task RefreshPanelsAsync()
    {
        return _refreshPanelsAsync();
    }

    [RelayCommand]
    private Task AddFileAsync()
    {
        return _addFileAsync();
    }

    [RelayCommand]
    private Task AddFolderAsync()
    {
        return _addFolderAsync();
    }

    [RelayCommand]
    private Task AddSeparatorAsync()
    {
        return _addSeparatorAsync();
    }

    [RelayCommand]
    private Task ImportPinnedShortcutsAsync()
    {
        return _importPinnedShortcutsAsync();
    }

    [RelayCommand]
    private Task OpenGroupsEditorAsync()
    {
        return _openGroupsEditorAsync();
    }

    [RelayCommand]
    private Task OpenLaunchProfilesEditorAsync()
    {
        return _openLaunchProfilesEditorAsync();
    }

    [RelayCommand]
    private Task RenamePanelAsync()
    {
        return _renamePanelAsync();
    }

    [RelayCommand]
    private Task DuplicatePanelAsync()
    {
        return _duplicatePanelAsync();
    }

    [RelayCommand]
    private Task CreateEmptyPanelAsync()
    {
        return _createEmptyPanelAsync();
    }

    [RelayCommand]
    private Task RemovePanelAsync()
    {
        return _removePanelAsync();
    }

    [RelayCommand]
    private Task HidePanelAsync()
    {
        return _hidePanelAsync();
    }

    [RelayCommand]
    private Task TogglePanelLockAsync()
    {
        return _togglePanelLockAsync();
    }

    [RelayCommand]
    private Task ToggleLabelsAsync()
    {
        return _toggleLabelsAsync();
    }

    [RelayCommand]
    private Task ToggleAlwaysOnTopAsync()
    {
        return _toggleAlwaysOnTopAsync();
    }

    [RelayCommand(CanExecute = nameof(CanAutoHide))]
    private Task ToggleAutoHideAsync()
    {
        return _toggleAutoHideAsync();
    }

    [RelayCommand]
    private Task SetHorizontalOrientationAsync()
    {
        return _setPanelOrientationAsync(PanelOrientation.Horizontal);
    }

    [RelayCommand]
    private Task SetVerticalOrientationAsync()
    {
        return _setPanelOrientationAsync(PanelOrientation.Vertical);
    }

    [RelayCommand]
    private Task IncreaseIconSizeAsync()
    {
        return _increaseIconSizeAsync();
    }

    [RelayCommand]
    private Task DecreaseIconSizeAsync()
    {
        return _decreaseIconSizeAsync();
    }

    [RelayCommand]
    private void Exit()
    {
        _exit();
    }

    private async Task LaunchSelectedItemAsync(DockPanelItemViewModel item)
    {
        await _activateItemAsync(item);
    }

    private async Task LaunchSelectedItemAsAdministratorAsync(DockPanelItemViewModel item)
    {
        var adminItem = new DockPanelItemViewModel(item.Id, item.DisplayName, item.Type, item.Target, item.Arguments, true, item.IconSource);
        await _activateItemAsync(adminItem);
    }

    private Task OpenSelectedItemLocationAsync(DockPanelItemViewModel item)
    {
        return _openLocationAsync(item);
    }

    private Task DuplicateSelectedItemAsync(DockPanelItemViewModel item)
    {
        return _duplicateAsync(item);
    }

    private Task RemoveSelectedItemAsync(DockPanelItemViewModel item)
    {
        return _removeAsync(item);
    }

    private Task DuplicateSelectedItemToNewPanelAsync(DockPanelItemViewModel item)
    {
        return _duplicateToNewPanelAsync(item);
    }

    private Task RenameSelectedItemAsync(DockPanelItemViewModel item)
    {
        return _renameAsync(item);
    }

    private Task EditSelectedItemAsync(DockPanelItemViewModel item)
    {
        return _editAsync(item);
    }

    private Task MoveSelectedItemToPanelAsync(DockPanelItemViewModel item, DockPanelMoveTargetViewModel target)
    {
        return _moveToPanelAsync(item, target);
    }

    private PlacementMode ResolveHoverLabelPlacement()
    {
        return Position switch
        {
            PanelPosition.Top => PlacementMode.Bottom,
            PanelPosition.Bottom => PlacementMode.Top,
            PanelPosition.Floating when Top < SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height / 2) => PlacementMode.Bottom,
            _ => PlacementMode.Top
        };
    }

    private Point ResolveDockItemTransformOrigin()
    {
        return Position switch
        {
            PanelPosition.Top => new Point(0.5, 0),
            PanelPosition.Bottom => new Point(0.5, 1),
            PanelPosition.Left => new Point(0, 0.5),
            PanelPosition.Right => new Point(1, 0.5),
            PanelPosition.Floating when Top < SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height / 2) => new Point(0.5, 0),
            _ => new Point(0.5, 1)
        };
    }

    private VerticalAlignment ResolveItemButtonVerticalAlignment()
    {
        return Position switch
        {
            PanelPosition.Top => VerticalAlignment.Top,
            PanelPosition.Bottom => VerticalAlignment.Bottom,
            PanelPosition.Left => VerticalAlignment.Center,
            PanelPosition.Right => VerticalAlignment.Center,
            PanelPosition.Floating when Top < SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height / 2) => VerticalAlignment.Top,
            _ => VerticalAlignment.Bottom
        };
    }

    private Brush CreatePanelBackgroundBrush()
    {
        var baseColor = ParseColor(PanelColor, Color.FromRgb(27, 38, 55));
        return new LinearGradientBrush(
            WithAlpha(Lighten(baseColor, 0.08), Opacity),
            WithAlpha(Darken(baseColor, 0.18), Opacity),
            new Point(0, 0),
            new Point(1, 1));
    }

    private Brush CreatePanelBorderBrush()
    {
        var baseColor = ParseColor(PanelColor, Color.FromRgb(27, 38, 55));
        return new SolidColorBrush(WithAlpha(Lighten(baseColor, 0.38), Math.Clamp(Opacity + 0.18, 0.45, 1)));
    }

    private Brush CreateIconHoverBackgroundBrush()
    {
        var baseColor = ParseColor(PanelColor, Color.FromRgb(27, 38, 55));
        return new SolidColorBrush(WithAlpha(Lighten(baseColor, 0.14), Math.Clamp(Opacity * 0.68, 0.20, 0.56)));
    }

    private Brush CreateIconHoverBorderBrush()
    {
        var baseColor = ParseColor(PanelColor, Color.FromRgb(27, 38, 55));
        return new SolidColorBrush(WithAlpha(Lighten(baseColor, 0.52), Math.Clamp(Opacity + 0.12, 0.52, 1)));
    }

    private static Color ParseColor(string value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            var parsed = ColorConverter.ConvertFromString(value);
            return parsed is Color color ? color : fallback;
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

    private static Color Lighten(Color color, double amount)
    {
        return Color.FromArgb(
            color.A,
            AdjustChannel(color.R, amount),
            AdjustChannel(color.G, amount),
            AdjustChannel(color.B, amount));
    }

    private static Color Darken(Color color, double amount)
    {
        return Color.FromArgb(
            color.A,
            AdjustChannel(color.R, -amount),
            AdjustChannel(color.G, -amount),
            AdjustChannel(color.B, -amount));
    }

    private static byte AdjustChannel(byte channel, double amount)
    {
        var next = amount >= 0
            ? channel + ((255 - channel) * amount)
            : channel * (1 + amount);
        return (byte)Math.Clamp((int)Math.Round(next, MidpointRounding.AwayFromZero), 0, 255);
    }

    private static Color WithAlpha(Color color, double opacity)
    {
        return Color.FromArgb(
            (byte)Math.Clamp((int)Math.Round(255 * opacity, MidpointRounding.AwayFromZero), 0, 255),
            color.R,
            color.G,
            color.B);
    }

}

public sealed record DockPanelMoveTargetViewModel(Guid PanelId, string PanelName);

public sealed partial class DockPanelItemViewModel : ObservableObject
{
    private Func<DockPanelItemViewModel, Task>? _launchAsync;
    private Func<DockPanelItemViewModel, Task>? _launchAsAdministratorAsync;
    private Func<DockPanelItemViewModel, Task>? _openLocationAsync;
    private Func<DockPanelItemViewModel, Task>? _duplicateAsync;
    private Func<DockPanelItemViewModel, Task>? _duplicateToNewPanelAsync;
    private Func<DockPanelItemViewModel, Task>? _removeAsync;
    private Func<DockPanelItemViewModel, Task>? _renameAsync;
    private Func<DockPanelItemViewModel, Task>? _editAsync;
    private Func<DockPanelItemViewModel, DockPanelMoveTargetViewModel, Task>? _moveToPanelAsync;

    public DockPanelItemViewModel(
        Guid id,
        string displayName,
        LauncherItemType type,
        string target,
        string? arguments,
        bool runAsAdministrator,
        ImageSource? iconSource,
        IReadOnlyList<DockPanelMoveTargetViewModel>? moveTargets = null)
    {
        Id = id;
        DisplayName = displayName;
        Type = type;
        Target = target;
        Arguments = arguments;
        RunAsAdministrator = runAsAdministrator;
        IconSource = iconSource;
        MoveTargets = moveTargets ?? [];
    }

    public Guid Id { get; }

    public string DisplayName { get; }

    public LauncherItemType Type { get; }

    public string TypeLabel => Type.ToString();

    public ImageSource? IconSource { get; }

    public bool IsSeparator => Type == LauncherItemType.Separator;

    public Visibility SeparatorVisibility => IsSeparator ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LauncherVisibility => IsSeparator ? Visibility.Collapsed : Visibility.Visible;

    public Visibility IconVisibility => IconSource is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility GlyphVisibility => IconSource is null ? Visibility.Visible : Visibility.Collapsed;

    public string Glyph => Type switch
    {
        LauncherItemType.Application => "APP",
        LauncherItemType.Shortcut => "LNK",
        LauncherItemType.Folder => "DIR",
        LauncherItemType.File => "FILE",
        LauncherItemType.Url => "WEB",
        LauncherItemType.Command => "CMD",
        LauncherItemType.Action => "ACT",
        LauncherItemType.Separator => "---",
        _ => "ITEM"
    };

    public Brush AccentBrush => Type switch
    {
        LauncherItemType.Application => CreateBrush("#FF2A9D8F"),
        LauncherItemType.Shortcut => CreateBrush("#FF3A86FF"),
        LauncherItemType.Folder => CreateBrush("#FFE9C46A"),
        LauncherItemType.File => CreateBrush("#FF8D99AE"),
        LauncherItemType.Url => CreateBrush("#FF06D6A0"),
        LauncherItemType.Command => CreateBrush("#FFEF476F"),
        LauncherItemType.Action => CreateBrush("#FFF4A261"),
        LauncherItemType.Separator => CreateBrush("#FF64748B"),
        _ => CreateBrush("#FF9CA3AF")
    };

    public string Target { get; }

    public string? Arguments { get; }

    public bool RunAsAdministrator { get; }

    public IReadOnlyList<DockPanelMoveTargetViewModel> MoveTargets { get; }

    public Visibility MoveTargetsVisibility => MoveTargets.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

    public void AttachLauncher(Func<DockPanelItemViewModel, Task> launchAsync)
    {
        _launchAsync = launchAsync;
    }

    public void AttachContextActions(
        Func<DockPanelItemViewModel, Task> launchAsAdministratorAsync,
        Func<DockPanelItemViewModel, Task> openLocationAsync,
        Func<DockPanelItemViewModel, Task> duplicateAsync,
        Func<DockPanelItemViewModel, Task> duplicateToNewPanelAsync,
        Func<DockPanelItemViewModel, Task> removeAsync,
        Func<DockPanelItemViewModel, Task> renameAsync,
        Func<DockPanelItemViewModel, Task> editAsync,
        Func<DockPanelItemViewModel, DockPanelMoveTargetViewModel, Task> moveToPanelAsync)
    {
        _launchAsAdministratorAsync = launchAsAdministratorAsync;
        _openLocationAsync = openLocationAsync;
        _duplicateAsync = duplicateAsync;
        _duplicateToNewPanelAsync = duplicateToNewPanelAsync;
        _removeAsync = removeAsync;
        _renameAsync = renameAsync;
        _editAsync = editAsync;
        _moveToPanelAsync = moveToPanelAsync;
    }

    [RelayCommand]
    private Task LaunchAsync()
    {
        if (IsSeparator)
        {
            return Task.CompletedTask;
        }

        return _launchAsync is null ? Task.CompletedTask : _launchAsync(this);
    }

    [RelayCommand]
    private Task LaunchAsAdministratorAsync()
    {
        return _launchAsAdministratorAsync is null ? Task.CompletedTask : _launchAsAdministratorAsync(this);
    }

    [RelayCommand]
    private Task OpenLocationAsync()
    {
        return _openLocationAsync is null ? Task.CompletedTask : _openLocationAsync(this);
    }

    [RelayCommand]
    private Task DuplicateAsync()
    {
        return _duplicateAsync is null ? Task.CompletedTask : _duplicateAsync(this);
    }

    [RelayCommand]
    private Task DuplicateToNewPanelAsync()
    {
        return _duplicateToNewPanelAsync is null ? Task.CompletedTask : _duplicateToNewPanelAsync(this);
    }

    [RelayCommand]
    private Task RemoveAsync()
    {
        return _removeAsync is null ? Task.CompletedTask : _removeAsync(this);
    }

    [RelayCommand]
    private Task RenameAsync()
    {
        return _renameAsync is null ? Task.CompletedTask : _renameAsync(this);
    }

    [RelayCommand]
    private Task EditAsync()
    {
        return _editAsync is null ? Task.CompletedTask : _editAsync(this);
    }

    [RelayCommand]
    private Task MoveToPanelAsync(DockPanelMoveTargetViewModel? target)
    {
        return _moveToPanelAsync is null || target is null ? Task.CompletedTask : _moveToPanelAsync(this, target);
    }

    private static Brush CreateBrush(string color)
    {
        return (Brush)new BrushConverter().ConvertFromString(color)!;
    }
}
