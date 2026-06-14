using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLauncher.BuildingBlocks.Presentation.Wpf;
using DockLauncher.Modules.Panels.Domain;
using System.Windows;
using System.Windows.Media;

namespace DockLauncher.AppHost.Docking;

public sealed partial class FolderFlyoutWindowViewModel : ViewModelBase
{
    private readonly Func<string, Task> _openPathAsync;
    private readonly Func<string, Task> _openInExplorerAsync;
    private readonly Func<FolderFlyoutEntryViewModel, Task> _openEntryLocationAsync;

    public FolderFlyoutWindowViewModel(
        string currentPath,
        double left,
        double top,
        PanelIconShape iconShape,
        PanelFlyoutDisplayMode displayMode,
        Func<string, Task> openPathAsync,
        Func<string, Task> openInExplorerAsync,
        Func<FolderFlyoutEntryViewModel, Task> openEntryLocationAsync)
    {
        this.currentPath = currentPath;
        Left = left;
        Top = top;
        IconShape = iconShape;
        DisplayMode = displayMode;
        _openPathAsync = openPathAsync;
        _openInExplorerAsync = openInExplorerAsync;
        _openEntryLocationAsync = openEntryLocationAsync;
    }

    public ObservableCollection<FolderFlyoutEntryViewModel> Entries { get; } = [];

    [ObservableProperty]
    private string currentPath;

    public string CurrentFolderName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CurrentPath))
            {
                return "Folder";
            }

            var trimmedPath = CurrentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmedPath);
            return string.IsNullOrWhiteSpace(name) ? CurrentPath : name;
        }
    }

    public string EntryCountLabel => Entries.Count == 1 ? "1 item" : $"{Entries.Count} items";

    public PanelIconShape IconShape { get; }

    public PanelFlyoutDisplayMode DisplayMode { get; }

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

    public double Left { get; }

    public double Top { get; }

    public void SetEntries(IEnumerable<FolderFlyoutEntryViewModel> entries, string currentFolderPath)
    {
        CurrentPath = currentFolderPath;
        Entries.Clear();
        foreach (var entry in entries)
        {
            entry.AttachOpen(async item => await _openPathAsync(item.Path));
            entry.AttachContextActions(OpenEntryLocationAsync);
            Entries.Add(entry);
        }

        OnPropertyChanged(nameof(CurrentFolderName));
        OnPropertyChanged(nameof(EntryCountLabel));
    }

    partial void OnCurrentPathChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentFolderName));
    }

    [RelayCommand]
    private Task OpenInExplorerAsync()
    {
        return _openInExplorerAsync(CurrentPath);
    }

    private Task OpenEntryLocationAsync(FolderFlyoutEntryViewModel entry)
    {
        return _openEntryLocationAsync(entry);
    }
}

public sealed partial class FolderFlyoutEntryViewModel : ObservableObject
{
    private Func<FolderFlyoutEntryViewModel, Task>? _openAsync;
    private Func<FolderFlyoutEntryViewModel, Task>? _openLocationAsync;

    public FolderFlyoutEntryViewModel(string name, string path, bool isDirectory, ImageSource? iconSource)
    {
        Name = name;
        Path = path;
        IsDirectory = isDirectory;
        IconSource = iconSource;
    }

    public string Name { get; }

    public string Path { get; }

    public bool IsDirectory { get; }

    public string KindLabel => IsDirectory ? "Folder" : "File";

    public ImageSource? IconSource { get; }

    public Visibility IconVisibility => IconSource is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility GlyphVisibility => IconSource is null ? Visibility.Visible : Visibility.Collapsed;

    public string Glyph => IsDirectory ? "DIR" : "FILE";

    public Brush AccentBrush => IsDirectory
        ? CreateBrush("#FFE9C46A")
        : CreateBrush("#FF8D99AE");

    public void AttachOpen(Func<FolderFlyoutEntryViewModel, Task> openAsync)
    {
        _openAsync = openAsync;
    }

    public void AttachContextActions(Func<FolderFlyoutEntryViewModel, Task> openLocationAsync)
    {
        _openLocationAsync = openLocationAsync;
    }

    [RelayCommand]
    private Task OpenAsync()
    {
        return _openAsync is null ? Task.CompletedTask : _openAsync(this);
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
