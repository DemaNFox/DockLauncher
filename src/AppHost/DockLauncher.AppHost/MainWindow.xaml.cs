using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using DockLauncher.AppHost.Configuration;
using DockLauncher.AppHost.DragDrop;
using DockLauncher.Modules.Settings.Presentation.Wpf;

namespace DockLauncher.AppHost;

public partial class MainWindow : Window
{
    private Point? _itemDragStart;

    private bool _allowClose;

    public MainWindow(WorkspaceEditorViewModel workspaceViewModel)
    {
        InitializeComponent();
        WindowDisplayPolicy.Apply(this, new WindowDisplayPolicyOptions(RecenterOnLoad: true));
        DataContext = new MainWindowViewModel(workspaceViewModel);
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        if (DataContext is MainWindowViewModel { Workspace.HasUnsavedChanges: true } viewModel)
        {
            var result = MessageBox.Show(
                this,
                "Exit without saving changes? You can save, discard the previewed changes, or cancel and keep editing.",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel)
            {
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                await viewModel.Workspace.SaveCommand.ExecuteAsync(null);
            }
            else
            {
                await viewModel.Workspace.DiscardChangesAsync();
            }
        }

        Hide();
    }

    private void OnPanelItemsDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DroppedPathExtractor.HasPaths(e.Data)
            ? DragDropEffects.Copy
            : e.Data.GetDataPresent(typeof(LauncherItemEditorItemViewModel))
                ? DragDropEffects.Move
                : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnPanelItemsDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var droppedPaths = DroppedPathExtractor.ExtractPaths(e.Data);
        if (droppedPaths.Count > 0)
        {
            e.Handled = true;
            viewModel.Workspace.AddDroppedPathsToSelectedPanel(droppedPaths);
            return;
        }

        if (TryGetDraggedItem(e, out var draggedItem))
        {
            viewModel.Workspace.MoveItemWithinSelectedPanel(draggedItem.Id, viewModel.Workspace.PanelItems.Count - 1);
        }
    }

    private void OnItemCardPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _itemDragStart = e.GetPosition(this);
    }

    private void OnItemCardMouseMove(object sender, MouseEventArgs e)
    {
        if (_itemDragStart is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPosition = e.GetPosition(this);
        if (Math.Abs(currentPosition.X - _itemDragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(currentPosition.Y - _itemDragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (sender is FrameworkElement element && element.DataContext is LauncherItemEditorItemViewModel item)
        {
            var dataObject = new DataObject(typeof(LauncherItemEditorItemViewModel), item);
            System.Windows.DragDrop.DoDragDrop(element, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
        }

        _itemDragStart = null;
    }

    private void OnItemCardDragOver(object sender, DragEventArgs e)
    {
        e.Effects = IsSupportedDrag(e) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnItemCardDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || sender is not FrameworkElement { DataContext: LauncherItemEditorItemViewModel targetItem })
        {
            return;
        }

        if (TryGetDraggedItem(e, out var draggedItem))
        {
            var targetIndex = viewModel.Workspace.PanelItems.IndexOf(targetItem);
            viewModel.Workspace.MoveItemWithinSelectedPanel(draggedItem.Id, targetIndex);
        }
    }

    private void OnPanelListItemDragOver(object sender, DragEventArgs e)
    {
        e.Effects = IsSupportedDrag(e) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnPanelListItemDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || sender is not FrameworkElement { DataContext: PanelEditorItemViewModel panel })
        {
            return;
        }

        if (TryGetDraggedItem(e, out var draggedItem))
        {
            viewModel.Workspace.MoveItemToPanel(draggedItem.Id, panel.Id);
        }
    }

    private void OnWorkspaceCollectionDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DroppedPathExtractor.HasPaths(e.Data)
            ? DragDropEffects.Copy
            : e.Data.GetDataPresent(typeof(LauncherItemEditorItemViewModel))
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnGroupItemsDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var droppedPaths = DroppedPathExtractor.ExtractPaths(e.Data);
        if (droppedPaths.Count > 0)
        {
            e.Handled = true;
            viewModel.Workspace.AddDroppedPathsToSelectedGroup(droppedPaths);
            return;
        }

        if (TryGetDraggedItem(e, out var draggedItem))
        {
            e.Handled = true;
            viewModel.Workspace.AddItemToSelectedGroup(draggedItem.Id);
        }
    }

    private void OnLaunchProfileStepsDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var droppedPaths = DroppedPathExtractor.ExtractPaths(e.Data);
        if (droppedPaths.Count > 0)
        {
            e.Handled = true;
            viewModel.Workspace.AddDroppedPathsToSelectedLaunchProfile(droppedPaths);
            return;
        }

        if (TryGetDraggedItem(e, out var draggedItem))
        {
            viewModel.Workspace.AddItemToSelectedLaunchProfile(draggedItem.Id);
        }
    }

    private static bool TryGetDraggedItem(DragEventArgs e, out LauncherItemEditorItemViewModel item)
    {
        item = null!;
        if (!e.Data.GetDataPresent(typeof(LauncherItemEditorItemViewModel)))
        {
            return false;
        }

        var dragged = e.Data.GetData(typeof(LauncherItemEditorItemViewModel)) as LauncherItemEditorItemViewModel;
        if (dragged is null)
        {
            return false;
        }

        item = dragged;
        return true;
    }

    private static bool IsSupportedDrag(DragEventArgs e)
    {
        return e.Data.GetDataPresent(typeof(LauncherItemEditorItemViewModel))
            || DroppedPathExtractor.HasPaths(e.Data);
    }
}

public sealed partial class MainWindowViewModel : ObservableObject
{
    public const int DesignTabIndex = 0;
    public const int GroupsTabIndex = 1;
    public const int LaunchProfilesTabIndex = 2;

    public MainWindowViewModel(WorkspaceEditorViewModel workspace)
    {
        Workspace = workspace;
    }

    public WorkspaceEditorViewModel Workspace { get; }

    [ObservableProperty]
    private int selectedEditorTabIndex;

    public async Task InitializeAsync()
    {
        await Workspace.LoadAsync();
    }

    public void ShowGroupsEditor()
    {
        SelectedEditorTabIndex = GroupsTabIndex;
    }

    public void ShowLaunchProfilesEditor()
    {
        SelectedEditorTabIndex = LaunchProfilesTabIndex;
    }
}
