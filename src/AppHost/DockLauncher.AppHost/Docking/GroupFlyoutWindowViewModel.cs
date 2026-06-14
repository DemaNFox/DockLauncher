using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLauncher.BuildingBlocks.Presentation.Wpf;
using DockLauncher.Modules.Panels.Domain;
using System.Windows;
using System.Windows.Media;

namespace DockLauncher.AppHost.Docking;

public sealed partial class GroupFlyoutWindowViewModel : ViewModelBase
{
    private readonly Func<GroupFlyoutItemViewModel, Task> _launchAsync;
    private readonly Func<GroupFlyoutItemViewModel, Task> _launchAsAdministratorAsync;
    private readonly Func<GroupFlyoutItemViewModel, Task> _openLocationAsync;

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
        Func<GroupFlyoutItemViewModel, Task> openLocationAsync)
    {
        Title = title.Trim();
        GroupIconSource = groupIconSource;
        Left = left;
        Top = top;
        IconShape = iconShape;
        DisplayMode = displayMode;
        OpenMode = openMode;
        Items = items;
        var windowSize = CalculateWindowSize(items.Count, displayMode, openMode);
        WindowWidth = windowSize.Width;
        WindowHeight = windowSize.Height;
        _launchAsync = launchAsync;
        _launchAsAdministratorAsync = launchAsAdministratorAsync;
        _openLocationAsync = openLocationAsync;

        foreach (var item in Items)
        {
            item.AttachLauncher(LaunchItemAsync);
            item.AttachContextActions(LaunchItemAsAdministratorAsync, OpenLocationAsync);
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
        var workArea = SystemParameters.WorkArea;
        var maxHeight = Math.Max(260, Math.Floor(workArea.Height * 0.78));
        var width = openMode == PanelGroupOpenMode.Floating ? 460d : 420d;
        const double chromeVerticalInset = 56d;
        const double headerHeight = 52d;
        const double contentBottomInset = 14d;
        var contentHeight = displayMode == PanelFlyoutDisplayMode.List
            ? Math.Max(90, itemCount * 66d)
            : Math.Max(98, Math.Ceiling(Math.Max(itemCount, 1) / 4d) * 98d);
        var requestedHeight = chromeVerticalInset + headerHeight + contentHeight + contentBottomInset;
        return new Size(width, Math.Min(maxHeight, requestedHeight));
    }

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
}

public sealed partial class GroupFlyoutItemViewModel : ObservableObject
{
    private Func<GroupFlyoutItemViewModel, Task>? _launchAsync;
    private Func<GroupFlyoutItemViewModel, Task>? _launchAsAdministratorAsync;
    private Func<GroupFlyoutItemViewModel, Task>? _openLocationAsync;

    public GroupFlyoutItemViewModel(Guid id, string displayName, string target, ImageSource? iconSource, string kindLabel)
    {
        Id = id;
        DisplayName = displayName;
        Target = target;
        IconSource = iconSource;
        KindLabel = kindLabel;
    }

    public Guid Id { get; }

    public string DisplayName { get; }

    public string Target { get; }

    public string KindLabel { get; }

    public ImageSource? IconSource { get; }

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
        Func<GroupFlyoutItemViewModel, Task> openLocationAsync)
    {
        _launchAsAdministratorAsync = launchAsAdministratorAsync;
        _openLocationAsync = openLocationAsync;
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

    private static Brush CreateBrush(string color)
    {
        return (Brush)new BrushConverter().ConvertFromString(color)!;
    }
}
