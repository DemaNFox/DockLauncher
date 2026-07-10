using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLauncher.BuildingBlocks.Presentation.Wpf;
using DockLauncher.Modules.Items.Domain;
using DockLauncher.Modules.Panels.Domain;
using System.Windows;
using System.Windows.Media;

namespace DockLauncher.AppHost.Docking;

public readonly record struct GroupFlyoutLayout(
    Size WindowSize,
    int Columns,
    int Rows,
    double GridWidth,
    double GridHeight,
    bool VerticalScrollEnabled);

public sealed partial class GroupFlyoutWindowViewModel : ViewModelBase
{
    private readonly Func<GroupFlyoutItemViewModel, Task> _launchAsync;
    private readonly Func<GroupFlyoutItemViewModel, Task> _launchAsAdministratorAsync;
    private readonly Func<GroupFlyoutItemViewModel, Task> _openLocationAsync;
    private readonly Func<GroupFlyoutItemViewModel, Task> _duplicateAsync;
    private readonly Func<GroupFlyoutItemViewModel, Task> _duplicateToNewPanelAsync;
    private readonly Func<GroupFlyoutItemViewModel, Task> _removeAsync;
    private readonly Func<GroupFlyoutItemViewModel, Task> _renameAsync;
    private readonly Func<GroupFlyoutItemViewModel, Task> _editAsync;
    private readonly Func<GroupFlyoutItemViewModel, DockPanelMoveTargetViewModel, Task> _moveToPanelAsync;

    public GroupFlyoutWindowViewModel(
        string title,
        ImageSource? groupIconSource,
        double left,
        double top,
        PanelIconShape iconShape,
        PanelFlyoutDisplayMode displayMode,
        PanelGroupOpenMode openMode,
        IReadOnlyList<GroupFlyoutItemViewModel> items,
        Func<GroupFlyoutItemViewModel, Task> launchAsync,
        Func<GroupFlyoutItemViewModel, Task> launchAsAdministratorAsync,
        Func<GroupFlyoutItemViewModel, Task> openLocationAsync,
        Func<GroupFlyoutItemViewModel, Task> duplicateAsync,
        Func<GroupFlyoutItemViewModel, Task> duplicateToNewPanelAsync,
        Func<GroupFlyoutItemViewModel, Task> removeAsync,
        Func<GroupFlyoutItemViewModel, Task> renameAsync,
        Func<GroupFlyoutItemViewModel, Task> editAsync,
        Func<GroupFlyoutItemViewModel, DockPanelMoveTargetViewModel, Task> moveToPanelAsync)
    {
        Title = title.Trim();
        GroupIconSource = groupIconSource;
        Left = left;
        Top = top;
        IconShape = iconShape;
        DisplayMode = displayMode;
        OpenMode = openMode;
        Items = items;
        var layout = CalculateLayout(items.Count, displayMode, openMode);
        WindowWidth = layout.WindowSize.Width;
        WindowHeight = layout.WindowSize.Height;
        TileColumns = layout.Columns;
        TileGridWidth = layout.GridWidth;
        TileViewportWidth = layout.GridWidth + (layout.VerticalScrollEnabled ? ScrollBarWidth : 0);
        TileVerticalScrollBarVisibility = layout.VerticalScrollEnabled
            ? System.Windows.Controls.ScrollBarVisibility.Visible
            : System.Windows.Controls.ScrollBarVisibility.Disabled;
        _launchAsync = launchAsync;
        _launchAsAdministratorAsync = launchAsAdministratorAsync;
        _openLocationAsync = openLocationAsync;
        _duplicateAsync = duplicateAsync;
        _duplicateToNewPanelAsync = duplicateToNewPanelAsync;
        _removeAsync = removeAsync;
        _renameAsync = renameAsync;
        _editAsync = editAsync;
        _moveToPanelAsync = moveToPanelAsync;

        foreach (var item in Items)
        {
            item.AttachLauncher(LaunchItemAsync);
            item.AttachContextActions(
                LaunchItemAsAdministratorAsync,
                OpenLocationAsync,
                DuplicateAsync,
                DuplicateToNewPanelAsync,
                RemoveAsync,
                RenameAsync,
                EditAsync,
                MoveToPanelAsync);
        }
    }

    public string Title { get; }

    public ImageSource? GroupIconSource { get; }

    public Visibility GroupIconVisibility => GroupIconSource is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility GroupGlyphVisibility => GroupIconSource is null ? Visibility.Visible : Visibility.Collapsed;

    public double Left { get; }

    public double Top { get; }

    public IReadOnlyList<GroupFlyoutItemViewModel> Items { get; }

    public string ItemCountLabel => Items.Count == 1 ? "1 item" : $"{Items.Count} items";

    public PanelIconShape IconShape { get; }

    public PanelFlyoutDisplayMode DisplayMode { get; }

    public PanelGroupOpenMode OpenMode { get; }

    public double WindowWidth { get; }

    public double WindowHeight { get; }

    public int TileColumns { get; }

    public double TileGridWidth { get; }

    public double TileViewportWidth { get; }

    public System.Windows.Controls.ScrollBarVisibility TileVerticalScrollBarVisibility { get; }

    public bool IsFloatingMode => OpenMode == PanelGroupOpenMode.Floating;

    public Visibility TilesVisibility => DisplayMode == PanelFlyoutDisplayMode.Tiles ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ListVisibility => DisplayMode == PanelFlyoutDisplayMode.List ? Visibility.Visible : Visibility.Collapsed;

    public CornerRadius FlyoutIconCornerRadius => IconShape switch
    {
        PanelIconShape.Circle => new CornerRadius(26),
        PanelIconShape.RoundedSquare => new CornerRadius(12),
        PanelIconShape.Square => new CornerRadius(0),
        PanelIconShape.Hexagon => new CornerRadius(14),
        _ => new CornerRadius(26)
    };

    public static Size CalculateWindowSize(int itemCount, PanelFlyoutDisplayMode displayMode, PanelGroupOpenMode openMode)
    {
        return CalculateLayout(itemCount, displayMode, openMode).WindowSize;
    }

    public static GroupFlyoutLayout CalculateLayout(
        int itemCount,
        PanelFlyoutDisplayMode displayMode,
        PanelGroupOpenMode openMode)
    {
        var workArea = SystemParameters.WorkArea;
        var maxModalWidth = Math.Max(MinimumWindowWidth, Math.Floor(workArea.Width * 0.9));
        var maxModalHeight = Math.Max(260, Math.Floor(workArea.Height * 0.9));
        var normalizedCount = Math.Max(itemCount, 1);

        if (displayMode == PanelFlyoutDisplayMode.List)
        {
            var width = Math.Min(maxModalWidth, openMode == PanelGroupOpenMode.Floating ? 460d : 420d);
            var requestedHeight = FixedVerticalSize + Math.Max(90, normalizedCount * 66d);
            return new GroupFlyoutLayout(
                new Size(width, Math.Min(maxModalHeight, requestedHeight)),
                1,
                normalizedCount,
                Math.Max(1, width - ChromeHorizontalInset),
                Math.Max(90, requestedHeight - FixedVerticalSize),
                requestedHeight > maxModalHeight);
        }

        var maximumColumns = Math.Max(
            1,
            (int)Math.Floor((maxModalWidth - ChromeHorizontalInset) / TileSlotWidth));
        var maximumVisibleRows = Math.Max(
            1,
            (int)Math.Floor((maxModalHeight - FixedVerticalSize) / TileSlotHeight));
        var fullLayouts = new List<GroupFlyoutLayoutCandidate>();
        var scrollingLayouts = new List<GroupFlyoutLayoutCandidate>();

        for (var columns = 1; columns <= maximumColumns; columns++)
        {
            var rows = (int)Math.Ceiling(normalizedCount / (double)columns);
            var needsScroll = rows > maximumVisibleRows;
            var visibleRows = needsScroll ? maximumVisibleRows : rows;
            var gridWidth = columns * TileSlotWidth;
            var gridHeight = visibleRows * TileSlotHeight;
            var viewportWidth = gridWidth + (needsScroll ? ScrollBarWidth : 0);
            var modalWidth = Math.Max(MinimumWindowWidth, ChromeHorizontalInset + viewportWidth);
            var modalHeight = Math.Min(maxModalHeight, FixedVerticalSize + gridHeight);
            if (modalWidth > maxModalWidth || modalHeight > maxModalHeight)
            {
                continue;
            }

            var candidate = new GroupFlyoutLayoutCandidate(
                columns,
                rows,
                visibleRows,
                gridWidth,
                gridHeight,
                modalWidth,
                modalHeight,
                needsScroll,
                CalculateVisualScore(normalizedCount, columns, rows, visibleRows, modalWidth, modalHeight, needsScroll));
            (needsScroll ? scrollingLayouts : fullLayouts).Add(candidate);
        }

        var candidates = fullLayouts.Count > 0 ? fullLayouts : scrollingLayouts;
        var best = candidates
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Columns)
            .First();

        return new GroupFlyoutLayout(
            new Size(best.ModalWidth, best.ModalHeight),
            best.Columns,
            best.Rows,
            best.GridWidth,
            best.GridHeight,
            best.NeedsScroll);
    }

    private const double TileSlotWidth = 104d;
    private const double TileSlotHeight = 106d;
    private const double ScrollBarWidth = 8d;
    private const double MinimumWindowWidth = 280d;
    private const double ChromeHorizontalInset = 60d;
    private const double FixedVerticalSize = 121d;
    private const double TargetAspectRatio = 1.25d;

    private static double CalculateVisualScore(
        int itemCount,
        int columns,
        int rows,
        int visibleRows,
        double modalWidth,
        double modalHeight,
        bool needsScroll)
    {
        var aspectRatio = modalWidth / modalHeight;
        var aspectScore = Math.Abs(Math.Log(aspectRatio / TargetAspectRatio)) * 8;
        var shapeScore = Math.Abs(Math.Log(columns / (double)visibleRows)) * 0.75;
        var elongationScore = aspectRatio switch
        {
            < 0.75 => (0.75 - aspectRatio) * 12,
            > 2 => (aspectRatio - 2) * 8,
            _ => 0
        };

        var emptyCells = columns * rows - itemCount;
        var emptyCellScore = needsScroll ? 0 : emptyCells / (double)(columns * rows) * 4;
        var lastRowItems = itemCount - columns * (rows - 1);
        var weakLastRowScore = !needsScroll && lastRowItems * 2 < columns ? 6 : 0;
        return aspectScore + shapeScore + elongationScore + emptyCellScore + weakLastRowScore;
    }

    private sealed record GroupFlyoutLayoutCandidate(
        int Columns,
        int Rows,
        int VisibleRows,
        double GridWidth,
        double GridHeight,
        double ModalWidth,
        double ModalHeight,
        bool NeedsScroll,
        double Score);

    private Task LaunchItemAsync(GroupFlyoutItemViewModel item)
    {
        return _launchAsync(item);
    }

    private Task LaunchItemAsAdministratorAsync(GroupFlyoutItemViewModel item)
    {
        return _launchAsAdministratorAsync(item);
    }

    private Task OpenLocationAsync(GroupFlyoutItemViewModel item)
    {
        return _openLocationAsync(item);
    }

    private Task DuplicateAsync(GroupFlyoutItemViewModel item) => _duplicateAsync(item);

    private Task DuplicateToNewPanelAsync(GroupFlyoutItemViewModel item) => _duplicateToNewPanelAsync(item);

    private Task RemoveAsync(GroupFlyoutItemViewModel item) => _removeAsync(item);

    private Task RenameAsync(GroupFlyoutItemViewModel item) => _renameAsync(item);

    private Task EditAsync(GroupFlyoutItemViewModel item) => _editAsync(item);

    private Task MoveToPanelAsync(GroupFlyoutItemViewModel item, DockPanelMoveTargetViewModel target) =>
        _moveToPanelAsync(item, target);
}

public sealed partial class GroupFlyoutItemViewModel : ObservableObject
{
    private Func<GroupFlyoutItemViewModel, Task>? _launchAsync;
    private Func<GroupFlyoutItemViewModel, Task>? _launchAsAdministratorAsync;
    private Func<GroupFlyoutItemViewModel, Task>? _openLocationAsync;
    private Func<GroupFlyoutItemViewModel, Task>? _duplicateAsync;
    private Func<GroupFlyoutItemViewModel, Task>? _duplicateToNewPanelAsync;
    private Func<GroupFlyoutItemViewModel, Task>? _removeAsync;
    private Func<GroupFlyoutItemViewModel, Task>? _renameAsync;
    private Func<GroupFlyoutItemViewModel, Task>? _editAsync;
    private Func<GroupFlyoutItemViewModel, DockPanelMoveTargetViewModel, Task>? _moveToPanelAsync;

    public GroupFlyoutItemViewModel(
        Guid id,
        string displayName,
        string target,
        ImageSource? iconSource,
        string kindLabel,
        IReadOnlyList<DockPanelMoveTargetViewModel>? moveTargets = null)
    {
        Id = id;
        DisplayName = displayName;
        Target = target;
        IconSource = iconSource;
        KindLabel = kindLabel;
        MoveTargets = moveTargets ?? [];
    }

    public Guid Id { get; }

    public string DisplayName { get; }

    public string Target { get; }

    public string KindLabel { get; }

    public ImageSource? IconSource { get; }

    public IReadOnlyList<DockPanelMoveTargetViewModel> MoveTargets { get; }

    public Visibility MoveTargetsVisibility => MoveTargets.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

    public Visibility OpenLocationVisibility =>
        Enum.TryParse<LauncherItemType>(KindLabel, out var type)
        && DockPanelItemViewModel.SupportsOpenLocation(type, Target)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility IconVisibility => IconSource is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility GlyphVisibility => IconSource is null ? Visibility.Visible : Visibility.Collapsed;

    public string Glyph => KindLabel switch
    {
        "Folder" => "DIR",
        "Url" => "WEB",
        "Action" => "ACT",
        "Shortcut" => "LNK",
        "Application" => "APP",
        "Command" => "CMD",
        _ => "FILE"
    };

    public Brush AccentBrush => KindLabel switch
    {
        "Folder" => CreateBrush("#FFE9C46A"),
        "Url" => CreateBrush("#FF06D6A0"),
        "Action" => CreateBrush("#FFF4A261"),
        "Shortcut" => CreateBrush("#FF3A86FF"),
        "Application" => CreateBrush("#FF2A9D8F"),
        "Command" => CreateBrush("#FFEF476F"),
        _ => CreateBrush("#FF8D99AE")
    };

    public void AttachLauncher(Func<GroupFlyoutItemViewModel, Task> launchAsync)
    {
        _launchAsync = launchAsync;
    }

    public void AttachContextActions(
        Func<GroupFlyoutItemViewModel, Task> launchAsAdministratorAsync,
        Func<GroupFlyoutItemViewModel, Task> openLocationAsync,
        Func<GroupFlyoutItemViewModel, Task> duplicateAsync,
        Func<GroupFlyoutItemViewModel, Task> duplicateToNewPanelAsync,
        Func<GroupFlyoutItemViewModel, Task> removeAsync,
        Func<GroupFlyoutItemViewModel, Task> renameAsync,
        Func<GroupFlyoutItemViewModel, Task> editAsync,
        Func<GroupFlyoutItemViewModel, DockPanelMoveTargetViewModel, Task> moveToPanelAsync)
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
    private Task DuplicateAsync() => _duplicateAsync is null ? Task.CompletedTask : _duplicateAsync(this);

    [RelayCommand]
    private Task DuplicateToNewPanelAsync() =>
        _duplicateToNewPanelAsync is null ? Task.CompletedTask : _duplicateToNewPanelAsync(this);

    [RelayCommand]
    private Task RemoveAsync() => _removeAsync is null ? Task.CompletedTask : _removeAsync(this);

    [RelayCommand]
    private Task RenameAsync() => _renameAsync is null ? Task.CompletedTask : _renameAsync(this);

    [RelayCommand]
    private Task EditAsync() => _editAsync is null ? Task.CompletedTask : _editAsync(this);

    [RelayCommand]
    private Task MoveToPanelAsync(DockPanelMoveTargetViewModel? target) =>
        _moveToPanelAsync is null || target is null ? Task.CompletedTask : _moveToPanelAsync(this, target);

    private static Brush CreateBrush(string color)
    {
        return (Brush)new BrushConverter().ConvertFromString(color)!;
    }
}
