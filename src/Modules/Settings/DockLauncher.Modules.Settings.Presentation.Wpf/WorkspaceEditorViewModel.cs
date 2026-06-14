using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using DockLauncher.BuildingBlocks.Application.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockLauncher.BuildingBlocks.Presentation.Wpf;
using DockLauncher.Modules.Groups.Domain;
using DockLauncher.Modules.Items.Application;
using DockLauncher.Modules.Items.Domain;
using DockLauncher.Modules.LaunchProfiles.Domain;
using DockLauncher.Modules.Panels.Domain;
using DockLauncher.Modules.Settings.Application;
using DockLauncher.Modules.Settings.Domain;
using System.Windows;
using System.Windows.Media;

namespace DockLauncher.Modules.Settings.Presentation.Wpf;

public sealed partial class WorkspaceEditorViewModel : ViewModelBase
{
    private const string DefaultPanelName = "New Panel";
    private const string DefaultItemName = "New Item";
    private const string DefaultItemTarget = "";

    private readonly LoadWorkspaceQueryHandler _loadWorkspaceQueryHandler;
    private readonly SaveWorkspaceCommandHandler _saveWorkspaceCommandHandler;
    private readonly LaunchItemCommandHandler _launchItemCommandHandler;
    private readonly IDockShellController _dockShellController;
    private readonly IItemIconProvider _iconProvider;
    private readonly IItemTargetPicker _itemTargetPicker;
    private readonly IPanelColorPicker _panelColorPicker;
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IWorkspaceTransferPicker _workspaceTransferPicker;
    private readonly List<LauncherItemEditorItemViewModel> _allItems = [];
    private CancellationTokenSource? _previewCancellationTokenSource;
    private bool _suppressChangeTracking;
    private bool _isLoaded;
    private int _previewRequestVersion;

    public WorkspaceEditorViewModel(
        LoadWorkspaceQueryHandler loadWorkspaceQueryHandler,
        SaveWorkspaceCommandHandler saveWorkspaceCommandHandler,
        LaunchItemCommandHandler launchItemCommandHandler,
        IDockShellController dockShellController,
        IItemIconProvider iconProvider,
        IItemTargetPicker itemTargetPicker,
        IPanelColorPicker panelColorPicker,
        IWorkspaceStore workspaceStore,
        IWorkspaceTransferPicker workspaceTransferPicker)
    {
        _loadWorkspaceQueryHandler = loadWorkspaceQueryHandler;
        _saveWorkspaceCommandHandler = saveWorkspaceCommandHandler;
        _launchItemCommandHandler = launchItemCommandHandler;
        _dockShellController = dockShellController;
        _iconProvider = iconProvider;
        _itemTargetPicker = itemTargetPicker;
        _panelColorPicker = panelColorPicker;
        _workspaceStore = workspaceStore;
        _workspaceTransferPicker = workspaceTransferPicker;

        Panels.CollectionChanged += OnPanelsCollectionChanged;
        Groups.CollectionChanged += OnGroupsCollectionChanged;
        LaunchProfiles.CollectionChanged += OnLaunchProfilesCollectionChanged;
    }

    public ObservableCollection<PanelEditorItemViewModel> Panels { get; } = [];

    public ObservableCollection<LauncherItemEditorItemViewModel> PanelItems { get; } = [];

    public ObservableCollection<GroupEditorItemViewModel> Groups { get; } = [];

    public ObservableCollection<LauncherItemEditorItemViewModel> GroupItems { get; } = [];

    public ObservableCollection<LauncherItemEditorItemViewModel> GroupSourcePanelItems { get; } = [];

    public ObservableCollection<LaunchProfileEditorItemViewModel> LaunchProfiles { get; } = [];

    public ObservableCollection<LaunchProfileStepEditorItemViewModel> SelectedLaunchProfileSteps { get; } = [];

    public ObservableCollection<LauncherItemEditorItemViewModel> LaunchProfileSourcePanelItems { get; } = [];

    public IReadOnlyList<LauncherItemType> ItemTypes { get; } = Enum.GetValues<LauncherItemType>();

    public IReadOnlyList<BuiltInDockActionPreset> BuiltInActions { get; } = BuiltInDockActions.Presets;

    public IReadOnlyList<PanelPosition> PanelPositions { get; } = Enum.GetValues<PanelPosition>();

    public IReadOnlyList<PanelLayoutMode> PanelLayoutModes { get; } = Enum.GetValues<PanelLayoutMode>();

    public IReadOnlyList<PanelOrientation> PanelOrientations { get; } = Enum.GetValues<PanelOrientation>();

    public IReadOnlyList<PanelLabelDisplayMode> PanelLabelDisplayModes { get; } = Enum.GetValues<PanelLabelDisplayMode>();

    public IReadOnlyList<PanelLabelPlacement> PanelLabelPlacements { get; } = Enum.GetValues<PanelLabelPlacement>();

    public IReadOnlyList<PanelIconShape> PanelIconShapes { get; } = Enum.GetValues<PanelIconShape>();

    public IReadOnlyList<PanelFlyoutDisplayMode> PanelFlyoutDisplayModes { get; } = Enum.GetValues<PanelFlyoutDisplayMode>();

    public IReadOnlyList<PanelGroupOpenMode> PanelGroupOpenModes { get; } = Enum.GetValues<PanelGroupOpenMode>();

    public IReadOnlyList<PanelOverflowMode> PanelOverflowModes { get; } = Enum.GetValues<PanelOverflowMode>();

    [ObservableProperty]
    private PanelEditorItemViewModel? selectedPanel;

    [ObservableProperty]
    private LauncherItemEditorItemViewModel? selectedPanelItem;

    [ObservableProperty]
    private PanelEditorItemViewModel? selectedMoveTargetPanel;

    [ObservableProperty]
    private GroupEditorItemViewModel? selectedGroup;

    [ObservableProperty]
    private LauncherItemEditorItemViewModel? selectedGroupItem;

    [ObservableProperty]
    private LauncherItemEditorItemViewModel? selectedGroupSourceItem;

    [ObservableProperty]
    private PanelEditorItemViewModel? selectedGroupSourcePanel;

    [ObservableProperty]
    private LaunchProfileEditorItemViewModel? selectedLaunchProfile;

    [ObservableProperty]
    private LaunchProfileStepEditorItemViewModel? selectedLaunchProfileStep;

    [ObservableProperty]
    private LauncherItemEditorItemViewModel? selectedLaunchProfileSourceItem;

    [ObservableProperty]
    private PanelEditorItemViewModel? selectedLaunchProfileSourcePanel;

    [ObservableProperty]
    private string draftPanelName = DefaultPanelName;

    [ObservableProperty]
    private string draftGroupName = "New Group";

    [ObservableProperty]
    private string draftLaunchProfileName = "New Launch Profile";

    [ObservableProperty]
    private string draftItemName = DefaultItemName;

    [ObservableProperty]
    private string draftItemTarget = DefaultItemTarget;

    [ObservableProperty]
    private LauncherItemType draftItemType = LauncherItemType.Application;

    [ObservableProperty]
    private BuiltInDockActionPreset? selectedBuiltInAction = BuiltInDockActions.Presets.FirstOrDefault();

    [ObservableProperty]
    private string summary = "Workspace not loaded.";

    [ObservableProperty]
    private string statusMessage = "Ready.";

    [ObservableProperty]
    private bool hasUnsavedChanges;

    partial void OnSelectedPanelChanged(PanelEditorItemViewModel? value)
    {
        if (SelectedMoveTargetPanel?.Id == value?.Id || SelectedMoveTargetPanel is null)
        {
            SelectedMoveTargetPanel = Panels.FirstOrDefault(panel => panel.Id != value?.Id);
        }

        RefreshPanelItems();
        AddItemToSelectedPanelCommand.NotifyCanExecuteChanged();
        MoveSelectedItemToPanelCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPanelItemChanged(LauncherItemEditorItemViewModel? value)
    {
        RemoveSelectedItemCommand.NotifyCanExecuteChanged();
        LaunchSelectedItemCommand.NotifyCanExecuteChanged();
        MoveSelectedItemToPanelCommand.NotifyCanExecuteChanged();
        AddSelectedItemToGroupCommand.NotifyCanExecuteChanged();
        RemoveSelectedItemFromGroupCommand.NotifyCanExecuteChanged();
        AddSelectedItemToLaunchProfileCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedMoveTargetPanelChanged(PanelEditorItemViewModel? value)
    {
        MoveSelectedItemToPanelCommand.NotifyCanExecuteChanged();
    }

    partial void OnDraftItemTargetChanged(string value)
    {
        AddItemToSelectedPanelCommand.NotifyCanExecuteChanged();

        if (DraftItemType == LauncherItemType.Action)
        {
            SelectedBuiltInAction = BuiltInActions.FirstOrDefault(action => string.Equals(action.Target, value, StringComparison.OrdinalIgnoreCase));
        }
    }

    partial void OnDraftItemTypeChanged(LauncherItemType value)
    {
        if (value == LauncherItemType.Action && SelectedBuiltInAction is not null)
        {
            ApplyBuiltInActionDraft(SelectedBuiltInAction);
        }
    }

    partial void OnSelectedBuiltInActionChanged(BuiltInDockActionPreset? value)
    {
        if (value is null)
        {
            return;
        }

        if (DraftItemType == LauncherItemType.Action)
        {
            ApplyBuiltInActionDraft(value);
        }
    }

    partial void OnSelectedGroupChanged(GroupEditorItemViewModel? value)
    {
        RefreshGroupItems();
        RefreshGroupSourcePanelItems();
        RemoveSelectedGroupCommand.NotifyCanExecuteChanged();
        AddSelectedItemToGroupCommand.NotifyCanExecuteChanged();
        RemoveSelectedItemFromGroupCommand.NotifyCanExecuteChanged();
        AddSelectedGroupToPanelCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedGroupItemChanged(LauncherItemEditorItemViewModel? value)
    {
        RemoveSelectedItemFromGroupCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedGroupSourceItemChanged(LauncherItemEditorItemViewModel? value)
    {
        AddSelectedSourceItemToGroupCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedGroupSourcePanelChanged(PanelEditorItemViewModel? value)
    {
        SelectSourcePanelForEditing(value);
        RefreshGroupSourcePanelItems();
    }

    partial void OnSelectedLaunchProfileChanged(LaunchProfileEditorItemViewModel? value)
    {
        RefreshLaunchProfileSteps();
        RefreshLaunchProfileSourcePanelItems();
        RemoveSelectedLaunchProfileCommand.NotifyCanExecuteChanged();
        AddSelectedItemToLaunchProfileCommand.NotifyCanExecuteChanged();
        RemoveSelectedStepFromLaunchProfileCommand.NotifyCanExecuteChanged();
        AddSelectedLaunchProfileToPanelCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedLaunchProfileStepChanged(LaunchProfileStepEditorItemViewModel? value)
    {
        RemoveSelectedStepFromLaunchProfileCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedLaunchProfileSourceItemChanged(LauncherItemEditorItemViewModel? value)
    {
        AddSelectedSourceItemToLaunchProfileCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedLaunchProfileSourcePanelChanged(PanelEditorItemViewModel? value)
    {
        SelectSourcePanelForEditing(value);
        RefreshLaunchProfileSourcePanelItems();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var workspace = await _loadWorkspaceQueryHandler.HandleAsync(new LoadWorkspaceQuery(), cancellationToken);
        ApplyWorkspaceToEditor(workspace, markUnsaved: false, $"Workspace loaded from disk at {DateTime.Now:T}.");
    }

    public Workspace CreateWorkspaceSnapshot()
    {
        return BuildWorkspace();
    }

    public async Task ApplyRuntimeWorkspaceAsync(Workspace workspace, string statusMessage, CancellationToken cancellationToken = default, Guid? selectedPanelId = null)
    {
        ApplyWorkspaceToEditor(workspace, markUnsaved: true, statusMessage, selectedPanelId);
        await _dockShellController.PreviewWorkspaceAsync(workspace, cancellationToken);
    }

    public Task ApplyRuntimeWorkspaceAsync(Workspace workspace, string statusMessage, CancellationToken cancellationToken)
    {
        return ApplyRuntimeWorkspaceAsync(workspace, statusMessage, cancellationToken, selectedPanelId: null);
    }

    private void ApplyWorkspaceToEditor(Workspace workspace, bool markUnsaved, string statusMessage, Guid? selectedPanelIdOverride = null)
    {
        _suppressChangeTracking = true;
        var selectedPanelId = selectedPanelIdOverride ?? SelectedPanel?.Id;
        var selectedGroupId = SelectedGroup?.Id;
        var selectedLaunchProfileId = SelectedLaunchProfile?.Id;

        try
        {
            Panels.Clear();
            Groups.Clear();
            LaunchProfiles.Clear();
            foreach (var panel in workspace.Panels)
            {
                var viewModel = new PanelEditorItemViewModel(
                    panel.Id,
                    panel.Name,
                    panel.Position,
                    panel.LayoutMode,
                    panel.Appearance.Opacity,
                    panel.Appearance.IconSize,
                    panel.Appearance.AlwaysOnTop,
                panel.Appearance.ResolvedLabelDisplayMode,
                panel.Appearance.ResolvedLabelPlacement,
                panel.Appearance.ResolvedIconShape,
                panel.Appearance.Orientation,
                panel.Appearance.HorizontalPadding,
                panel.Appearance.VerticalPadding,
                panel.Appearance.LabelSpacing,
                panel.Appearance.TextSize,
                panel.Appearance.ResolvedPanelColor,
                panel.Appearance.ResolvedFlyoutDisplayMode,
                panel.Appearance.GroupOpenMode,
                panel.Appearance.AutoHide,
                panel.Appearance.Locked,
                panel.Appearance.FloatingLeft,
                panel.Appearance.FloatingTop,
                panel.Appearance.DockOffset,
                panel.Appearance.ResolvedOverflowMode,
                panel.Appearance.ResolvedMaxOverflowTracks,
                panel.Appearance.IsHidden,
                panel.Appearance.CustomWidth,
                panel.Appearance.CustomHeight);
                foreach (var itemId in panel.ItemIds)
                {
                    viewModel.ItemIds.Add(itemId);
                }

                Panels.Add(viewModel);
            }

            foreach (var group in workspace.Groups)
            {
                var viewModel = new GroupEditorItemViewModel(group.Id, group.Name);
                foreach (var itemId in group.ItemIds)
                {
                    viewModel.ItemIds.Add(itemId);
                }

                Groups.Add(viewModel);
            }

            foreach (var launchProfile in workspace.LaunchProfiles)
            {
                var viewModel = new LaunchProfileEditorItemViewModel(launchProfile.Id, launchProfile.Name);
                foreach (var step in launchProfile.Steps)
                {
                    viewModel.Steps.Add(new LaunchProfileStepEditorItemViewModel(step.ItemId, step.DelayMs, step.RunAsAdministrator));
                }

                LaunchProfiles.Add(viewModel);
            }

            SelectedPanel = Panels.FirstOrDefault(panel => panel.Id == selectedPanelId) ?? Panels.FirstOrDefault();
            SelectedGroup = Groups.FirstOrDefault(group => group.Id == selectedGroupId) ?? Groups.FirstOrDefault();
            SelectedLaunchProfile = LaunchProfiles.FirstOrDefault(profile => profile.Id == selectedLaunchProfileId) ?? LaunchProfiles.FirstOrDefault();
            SelectedGroupSourcePanel = SelectedPanel ?? Panels.FirstOrDefault();
            SelectedLaunchProfileSourcePanel = SelectedPanel ?? Panels.FirstOrDefault();
            SelectedMoveTargetPanel = Panels.FirstOrDefault(panel => panel.Id != SelectedPanel?.Id);
            SetItemsFromWorkspace(workspace.Items);
            UpdateSummary(workspace);
            HasUnsavedChanges = markUnsaved;
            StatusMessage = statusMessage;
        }
        finally
        {
            _suppressChangeTracking = false;
            _isLoaded = true;
            _previewCancellationTokenSource?.Cancel();
            RefreshCommandStates();
        }
    }

    [RelayCommand]
    private void AddPanel()
    {
        var name = string.IsNullOrWhiteSpace(DraftPanelName) ? $"Panel {Panels.Count + 1}" : DraftPanelName.Trim();
        var panel = new PanelEditorItemViewModel(
            Guid.NewGuid(),
            name,
            PanelPosition.Bottom,
            PanelLayoutMode.IconWithLabel,
            0.9,
            40,
            true,
            PanelLabelDisplayMode.AlwaysVisible,
            PanelLabelPlacement.BelowIcon,
            PanelIconShape.Circle,
            null,
            20d,
            18d,
            4d,
            10.5d,
            "#1B2637",
            PanelFlyoutDisplayMode.Tiles,
            PanelGroupOpenMode.Floating,
            false,
            false,
            null,
            null,
            null,
            PanelOverflowMode.Scroll,
            1,
            false,
            null,
            null);
        Panels.Add(panel);
        SelectedPanel = panel;
        SelectedMoveTargetPanel ??= Panels.FirstOrDefault(candidate => candidate.Id != SelectedPanel.Id);
        DraftPanelName = $"Panel {Panels.Count + 1}";
        UpdateSummary();
        StatusMessage = $"Panel '{name}' added.";
        RemoveSelectedPanelCommand.NotifyCanExecuteChanged();
        AddItemToSelectedPanelCommand.NotifyCanExecuteChanged();
        MoveSelectedItemToPanelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedPanel))]
    private void RemoveSelectedPanel()
    {
        if (SelectedPanel is null)
        {
            return;
        }

        var removed = SelectedPanel;
        Panels.Remove(removed);
        SelectedPanel = Panels.FirstOrDefault();
        if (SelectedMoveTargetPanel?.Id == removed.Id)
        {
            SelectedMoveTargetPanel = Panels.FirstOrDefault(panel => panel.Id != SelectedPanel?.Id);
        }

        RefreshPanelItems();
        UpdateSummary();
        StatusMessage = $"Panel '{removed.Name}' removed.";
        RemoveSelectedPanelCommand.NotifyCanExecuteChanged();
        AddItemToSelectedPanelCommand.NotifyCanExecuteChanged();
        MoveSelectedItemToPanelCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedPanel()
    {
        return SelectedPanel is not null;
    }

    private void RefreshCommandStates()
    {
        RemoveSelectedPanelCommand.NotifyCanExecuteChanged();
        AddItemToSelectedPanelCommand.NotifyCanExecuteChanged();
        RemoveSelectedItemCommand.NotifyCanExecuteChanged();
        LaunchSelectedItemCommand.NotifyCanExecuteChanged();
        MoveSelectedItemToPanelCommand.NotifyCanExecuteChanged();
        RemoveSelectedGroupCommand.NotifyCanExecuteChanged();
        AddSelectedItemToGroupCommand.NotifyCanExecuteChanged();
        RemoveSelectedItemFromGroupCommand.NotifyCanExecuteChanged();
        AddSelectedGroupToPanelCommand.NotifyCanExecuteChanged();
        RemoveSelectedLaunchProfileCommand.NotifyCanExecuteChanged();
        AddSelectedItemToLaunchProfileCommand.NotifyCanExecuteChanged();
        RemoveSelectedStepFromLaunchProfileCommand.NotifyCanExecuteChanged();
        AddSelectedLaunchProfileToPanelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private void AddItemToSelectedPanel()
    {
        if (SelectedPanel is null)
        {
            return;
        }

        var item = CreateDraftItem(DraftItemTarget, DraftItemName, DraftItemType);
        AddItemToPanel(SelectedPanel, item);
        ResetDraftItem();
        StatusMessage = $"Item '{item.DisplayName}' added to panel '{SelectedPanel.Name}'.";
    }

    private bool CanAddItem()
    {
        return SelectedPanel is not null && !string.IsNullOrWhiteSpace(DraftItemTarget);
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedItem))]
    private void RemoveSelectedItem()
    {
        if (SelectedPanel is null || SelectedPanelItem is null)
        {
            return;
        }

        foreach (var group in Groups)
        {
            group.ItemIds.Remove(SelectedPanelItem.Id);
        }

        foreach (var profile in LaunchProfiles)
        {
            var staleSteps = profile.Steps.Where(step => step.ItemId == SelectedPanelItem.Id).ToArray();
            foreach (var staleStep in staleSteps)
            {
                profile.Steps.Remove(staleStep);
            }
        }

        SelectedPanel.ItemIds.Remove(SelectedPanelItem.Id);
        foreach (var item in _allItems.Where(item => item.Id == SelectedPanelItem.Id).ToArray())
        {
            item.PropertyChanged -= OnLauncherItemPropertyChanged;
            _allItems.Remove(item);
        }
        var removedName = SelectedPanelItem.DisplayName;
        SelectedPanelItem = null;
        RefreshPanelItems();
        RefreshGroupItems();
        RefreshLaunchProfileSteps();
        RefreshGroupSourcePanelItems();
        RefreshLaunchProfileSourcePanelItems();
        UpdateSummary();
        StatusMessage = $"Item '{removedName}' removed from panel '{SelectedPanel.Name}'.";
        MoveSelectedItemToPanelCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveSelectedItem()
    {
        return SelectedPanelItem is not null;
    }

    [RelayCommand(CanExecute = nameof(CanLaunchSelectedItem))]
    private async Task LaunchSelectedItemAsync()
    {
        if (SelectedPanelItem is null)
        {
            return;
        }

        var item = new LauncherItem(
            SelectedPanelItem.Id,
            SelectedPanelItem.DisplayName,
            SelectedPanelItem.Type,
            SelectedPanelItem.Target,
            SelectedPanelItem.Arguments,
            SelectedPanelItem.RunAsAdministrator);

        var result = await _launchItemCommandHandler.HandleAsync(new LaunchItemCommand(item), CancellationToken.None);
        StatusMessage = result.IsSuccess
            ? $"Launch requested for '{SelectedPanelItem.DisplayName}'."
            : $"Launch failed: {result.Error.Message}";
    }

    private bool CanLaunchSelectedItem()
    {
        return SelectedPanelItem is not null;
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedItem))]
    private void MoveSelectedItemToPanel()
    {
        if (SelectedPanel is null || SelectedPanelItem is null || SelectedMoveTargetPanel is null)
        {
            return;
        }

        if (SelectedMoveTargetPanel.Id == SelectedPanel.Id)
        {
            return;
        }

        SelectedPanel.ItemIds.Remove(SelectedPanelItem.Id);
        SelectedMoveTargetPanel.ItemIds.Add(SelectedPanelItem.Id);

        var movedItemName = SelectedPanelItem.DisplayName;
        var sourcePanelName = SelectedPanel.Name;
        var targetPanelName = SelectedMoveTargetPanel.Name;

        RefreshPanelItems();
        UpdateSummary();
        StatusMessage = $"Item '{movedItemName}' moved from '{sourcePanelName}' to '{targetPanelName}'.";
    }

    private bool CanMoveSelectedItem()
    {
        return SelectedPanelItem is not null
            && SelectedPanel is not null
            && SelectedMoveTargetPanel is not null
            && SelectedMoveTargetPanel.Id != SelectedPanel.Id;
    }

    [RelayCommand]
    private void AddGroup()
    {
        var name = string.IsNullOrWhiteSpace(DraftGroupName) ? $"Group {Groups.Count + 1}" : DraftGroupName.Trim();
        var group = new GroupEditorItemViewModel(Guid.NewGuid(), name);
        Groups.Add(group);
        SelectedGroup = group;
        DraftGroupName = $"Group {Groups.Count + 1}";
        StatusMessage = $"Group '{name}' created.";
        UpdateSummary();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedGroup))]
    private void RemoveSelectedGroup()
    {
        if (SelectedGroup is null)
        {
            return;
        }

        var removedName = SelectedGroup.Name;
        Groups.Remove(SelectedGroup);
        SelectedGroup = Groups.FirstOrDefault();
        StatusMessage = $"Group '{removedName}' removed.";
        UpdateSummary();
    }

    private bool CanRemoveSelectedGroup()
    {
        return SelectedGroup is not null;
    }

    public void AddItemToSelectedGroup(Guid itemId)
    {
        if (SelectedGroup is null)
        {
            return;
        }

        if (!SelectedGroup.ItemIds.Contains(itemId))
        {
            SelectedGroup.ItemIds.Add(itemId);
        }

        RefreshGroupItems();
        var itemName = _allItems.FirstOrDefault(item => item.Id == itemId)?.DisplayName ?? "Item";
        StatusMessage = $"Item '{itemName}' added to group '{SelectedGroup.Name}'.";
    }

    public void AddDroppedPathsToSelectedGroup(IEnumerable<string> paths)
    {
        if (SelectedGroup is null)
        {
            StatusMessage = "Select a group before dropping items.";
            return;
        }

        var addedCount = 0;
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var item = CreateDraftItem(path);
            AddWorkspaceItem(item);
            AddItemToSelectedGroup(item.Id);
            addedCount++;
        }

        if (addedCount > 0)
        {
            StatusMessage = $"{addedCount} item(s) added to group '{SelectedGroup.Name}'.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddSelectedItemToGroup))]
    private void AddSelectedItemToGroup()
    {
        if (SelectedGroup is null || SelectedPanelItem is null)
        {
            return;
        }

        AddItemToSelectedGroup(SelectedPanelItem.Id);
    }

    private bool CanAddSelectedItemToGroup()
    {
        return SelectedGroup is not null && SelectedPanelItem is not null;
    }

    [RelayCommand(CanExecute = nameof(CanAddSelectedSourceItemToGroup))]
    private void AddSelectedSourceItemToGroup()
    {
        if (SelectedGroupSourceItem is null)
        {
            return;
        }

        AddItemToSelectedGroup(SelectedGroupSourceItem.Id);
    }

    private bool CanAddSelectedSourceItemToGroup()
    {
        return SelectedGroup is not null && SelectedGroupSourceItem is not null;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedItemFromGroup))]
    private void RemoveSelectedItemFromGroup()
    {
        if (SelectedGroup is null || SelectedGroupItem is null)
        {
            return;
        }

        SelectedGroup.ItemIds.Remove(SelectedGroupItem.Id);
        var removedItemName = SelectedGroupItem.DisplayName;
        RefreshGroupItems();
        StatusMessage = $"Item '{removedItemName}' removed from group '{SelectedGroup.Name}'.";
    }

    private bool CanRemoveSelectedItemFromGroup()
    {
        return SelectedGroup is not null
            && SelectedGroupItem is not null
            && SelectedGroup.ItemIds.Contains(SelectedGroupItem.Id);
    }

    [RelayCommand(CanExecute = nameof(CanAddSelectedGroupToPanel))]
    private void AddSelectedGroupToPanel()
    {
        if (SelectedPanel is null || SelectedGroup is null)
        {
            return;
        }

        var existingLauncher = _allItems.FirstOrDefault(item => item.Target == BuildGroupTarget(SelectedGroup.Id));
        if (existingLauncher is null)
        {
            existingLauncher = new LauncherItemEditorItemViewModel(
                Guid.NewGuid(),
                SelectedGroup.Name,
                LauncherItemType.Action,
                BuildGroupTarget(SelectedGroup.Id),
                null,
                false,
                null,
                _iconProvider.GetIcon(LauncherItemType.Action, BuildGroupTarget(SelectedGroup.Id)));
            existingLauncher.PropertyChanged += OnLauncherItemPropertyChanged;
            _allItems.Add(existingLauncher);
        }
        else
        {
            existingLauncher.DisplayName = SelectedGroup.Name;
        }

        if (!SelectedPanel.ItemIds.Contains(existingLauncher.Id))
        {
            SelectedPanel.ItemIds.Add(existingLauncher.Id);
        }

        RefreshPanelItems();
        StatusMessage = $"Group '{SelectedGroup.Name}' added to panel '{SelectedPanel.Name}'.";
    }

    private bool CanAddSelectedGroupToPanel()
    {
        return SelectedPanel is not null && SelectedGroup is not null;
    }

    [RelayCommand]
    private void AddLaunchProfile()
    {
        var name = string.IsNullOrWhiteSpace(DraftLaunchProfileName) ? $"Launch Profile {LaunchProfiles.Count + 1}" : DraftLaunchProfileName.Trim();
        var profile = new LaunchProfileEditorItemViewModel(Guid.NewGuid(), name);
        LaunchProfiles.Add(profile);
        SelectedLaunchProfile = profile;
        DraftLaunchProfileName = $"Launch Profile {LaunchProfiles.Count + 1}";
        StatusMessage = $"Launch profile '{name}' created.";
        UpdateSummary();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedLaunchProfile))]
    private void RemoveSelectedLaunchProfile()
    {
        if (SelectedLaunchProfile is null)
        {
            return;
        }

        var target = BuildLaunchProfileTarget(SelectedLaunchProfile.Id);
        foreach (var panel in Panels)
        {
            var launcherIds = panel.ItemIds
                .Where(itemId => _allItems.Any(item => item.Id == itemId && string.Equals(item.Target, target, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            foreach (var launcherId in launcherIds)
            {
                panel.ItemIds.Remove(launcherId);
                foreach (var item in _allItems.Where(item => item.Id == launcherId).ToArray())
                {
                    item.PropertyChanged -= OnLauncherItemPropertyChanged;
                    _allItems.Remove(item);
                }
            }
        }

        var removedName = SelectedLaunchProfile.Name;
        LaunchProfiles.Remove(SelectedLaunchProfile);
        SelectedLaunchProfile = LaunchProfiles.FirstOrDefault();
        RefreshPanelItems();
        StatusMessage = $"Launch profile '{removedName}' removed.";
        UpdateSummary();
    }

    private bool CanRemoveSelectedLaunchProfile()
    {
        return SelectedLaunchProfile is not null;
    }

    public void AddItemToSelectedLaunchProfile(Guid itemId)
    {
        if (SelectedLaunchProfile is null)
        {
            return;
        }

        var existingItem = _allItems.FirstOrDefault(item => item.Id == itemId);
        var runAsAdministrator = existingItem?.RunAsAdministrator ?? false;
        SelectedLaunchProfile.Steps.Add(new LaunchProfileStepEditorItemViewModel(itemId, 0, runAsAdministrator));
        RefreshLaunchProfileSteps();
        var itemName = existingItem?.DisplayName ?? "Item";
        StatusMessage = $"Item '{itemName}' added to launch profile '{SelectedLaunchProfile.Name}'.";
    }

    public void AddDroppedPathsToSelectedLaunchProfile(IEnumerable<string> paths)
    {
        if (SelectedLaunchProfile is null)
        {
            StatusMessage = "Select a launch profile before dropping items.";
            return;
        }

        var addedCount = 0;
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var item = CreateDraftItem(path);
            AddWorkspaceItem(item);
            AddItemToSelectedLaunchProfile(item.Id);
            addedCount++;
        }

        if (addedCount > 0)
        {
            StatusMessage = $"{addedCount} item(s) added to launch profile '{SelectedLaunchProfile.Name}'.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddSelectedItemToLaunchProfile))]
    private void AddSelectedItemToLaunchProfile()
    {
        if (SelectedLaunchProfile is null || SelectedPanelItem is null)
        {
            return;
        }

        AddItemToSelectedLaunchProfile(SelectedPanelItem.Id);
    }

    private bool CanAddSelectedItemToLaunchProfile()
    {
        return SelectedLaunchProfile is not null && SelectedPanelItem is not null;
    }

    [RelayCommand(CanExecute = nameof(CanAddSelectedSourceItemToLaunchProfile))]
    private void AddSelectedSourceItemToLaunchProfile()
    {
        if (SelectedLaunchProfileSourceItem is null)
        {
            return;
        }

        AddItemToSelectedLaunchProfile(SelectedLaunchProfileSourceItem.Id);
    }

    private bool CanAddSelectedSourceItemToLaunchProfile()
    {
        return SelectedLaunchProfile is not null && SelectedLaunchProfileSourceItem is not null;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedStepFromLaunchProfile))]
    private void RemoveSelectedStepFromLaunchProfile()
    {
        if (SelectedLaunchProfile is null || SelectedLaunchProfileStep is null)
        {
            return;
        }

        var removedStep = SelectedLaunchProfileStep;
        SelectedLaunchProfile.Steps.Remove(removedStep);
        RefreshLaunchProfileSteps();
        StatusMessage = $"Step removed from launch profile '{SelectedLaunchProfile.Name}'.";
    }

    private bool CanRemoveSelectedStepFromLaunchProfile()
    {
        return SelectedLaunchProfile is not null
            && SelectedLaunchProfileStep is not null;
    }

    [RelayCommand(CanExecute = nameof(CanAddSelectedLaunchProfileToPanel))]
    private void AddSelectedLaunchProfileToPanel()
    {
        if (SelectedPanel is null || SelectedLaunchProfile is null)
        {
            return;
        }

        var existingLauncher = _allItems.FirstOrDefault(item => item.Target == BuildLaunchProfileTarget(SelectedLaunchProfile.Id));
        if (existingLauncher is null)
        {
            existingLauncher = new LauncherItemEditorItemViewModel(
                Guid.NewGuid(),
                SelectedLaunchProfile.Name,
                LauncherItemType.Action,
                BuildLaunchProfileTarget(SelectedLaunchProfile.Id),
                null,
                false,
                null,
                _iconProvider.GetIcon(LauncherItemType.Action, BuildLaunchProfileTarget(SelectedLaunchProfile.Id)));
            existingLauncher.PropertyChanged += OnLauncherItemPropertyChanged;
            _allItems.Add(existingLauncher);
        }
        else
        {
            existingLauncher.DisplayName = SelectedLaunchProfile.Name;
        }

        if (!SelectedPanel.ItemIds.Contains(existingLauncher.Id))
        {
            SelectedPanel.ItemIds.Add(existingLauncher.Id);
        }

        RefreshPanelItems();
        StatusMessage = $"Launch profile '{SelectedLaunchProfile.Name}' added to panel '{SelectedPanel.Name}'.";
    }

    private bool CanAddSelectedLaunchProfileToPanel()
    {
        return SelectedPanel is not null && SelectedLaunchProfile is not null;
    }

    [RelayCommand]
    private async Task BrowseFileAsync()
    {
        var path = await _itemTargetPicker.PickFileAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        ApplyDraftFromTarget(path);
        StatusMessage = $"Target selected: {path}";
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var path = await _itemTargetPicker.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        ApplyDraftFromTarget(path);
        StatusMessage = $"Folder selected: {path}";
    }

    [RelayCommand]
    private async Task PickPanelColorAsync()
    {
        if (SelectedPanel is null)
        {
            return;
        }

        var color = await _panelColorPicker.PickColorAsync(SelectedPanel.PanelColor, CancellationToken.None);
        if (string.IsNullOrWhiteSpace(color))
        {
            return;
        }

        SelectedPanel.PanelColor = color;
        StatusMessage = $"Panel color set to {color}.";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var workspace = BuildWorkspace();
        await _saveWorkspaceCommandHandler.HandleAsync(new SaveWorkspaceCommand(workspace), CancellationToken.None);
        await _dockShellController.RefreshAsync();
        UpdateSummary(workspace);
        HasUnsavedChanges = false;
        StatusMessage = $"Workspace saved at {DateTime.Now:T}.";
    }

    [RelayCommand]
    private Task ReloadAsync()
    {
        return LoadAsync();
    }

    public async Task DiscardChangesAsync(CancellationToken cancellationToken = default)
    {
        await LoadAsync(cancellationToken);
        await _dockShellController.RefreshAsync(cancellationToken);
        HasUnsavedChanges = false;
        StatusMessage = "Unsaved changes discarded.";
    }

    [RelayCommand]
    private async Task ExportWorkspaceAsync()
    {
        var path = await _workspaceTransferPicker.PickExportFileAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await _workspaceStore.ExportAsync(BuildWorkspace(), path, CancellationToken.None);
        StatusMessage = $"Workspace exported to '{path}'.";
    }

    [RelayCommand]
    private async Task ImportWorkspaceAsync()
    {
        var path = await _workspaceTransferPicker.PickImportFileAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var workspace = await _workspaceStore.ImportAsync(path, CancellationToken.None);
        await _saveWorkspaceCommandHandler.HandleAsync(new SaveWorkspaceCommand(workspace), CancellationToken.None);
        await LoadAsync();
        await _dockShellController.RefreshAsync();
        UpdateSummary(workspace);
        StatusMessage = $"Workspace imported from '{path}'.";
    }

    [RelayCommand]
    private async Task ResetLayoutAsync()
    {
        var workspace = await _workspaceStore.ResetAsync(CancellationToken.None);
        await LoadAsync();
        await _dockShellController.RefreshAsync();
        UpdateSummary(workspace);
        StatusMessage = "Workspace reset to default layout.";
    }

    public void AddDroppedPathsToSelectedPanel(IEnumerable<string> paths)
    {
        if (SelectedPanel is null)
        {
            StatusMessage = "Select a panel before dropping items.";
            return;
        }

        var addedCount = 0;
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var item = CreateDraftItem(path);
            AddItemToPanel(SelectedPanel, item);
            addedCount++;
        }

        if (addedCount == 0)
        {
            return;
        }

        StatusMessage = $"{addedCount} item(s) added to panel '{SelectedPanel.Name}' via drag and drop.";
    }

    public int AddDroppedPathsToPanel(Guid panelId, IEnumerable<string> paths)
    {
        var panel = Panels.FirstOrDefault(candidate => candidate.Id == panelId);
        if (panel is null)
        {
            StatusMessage = "Target panel was not found for drag and drop.";
            return 0;
        }

        var addedCount = 0;
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var item = CreateDraftItem(path);
            item.PropertyChanged += OnLauncherItemPropertyChanged;
            _allItems.Add(item);
            panel.ItemIds.Add(item.Id);
            addedCount++;
        }

        if (addedCount == 0)
        {
            return 0;
        }

        if (SelectedPanel?.Id == panel.Id)
        {
            RefreshPanelItems();
        }

        UpdateSummary();
        RemoveSelectedItemCommand.NotifyCanExecuteChanged();
        LaunchSelectedItemCommand.NotifyCanExecuteChanged();
        MoveSelectedItemToPanelCommand.NotifyCanExecuteChanged();
        StatusMessage = $"{addedCount} item(s) added to panel '{panel.Name}' via drag and drop.";
        MarkWorkspaceChanged();
        return addedCount;
    }

    public bool MoveItemWithinSelectedPanel(Guid itemId, int targetIndex)
    {
        if (SelectedPanel is null)
        {
            return false;
        }

        var sourceIndex = SelectedPanel.ItemIds.IndexOf(itemId);
        if (sourceIndex < 0)
        {
            return false;
        }

        targetIndex = Math.Clamp(targetIndex, 0, SelectedPanel.ItemIds.Count - 1);
        if (sourceIndex == targetIndex)
        {
            return false;
        }

        SelectedPanel.ItemIds.Move(sourceIndex, targetIndex);
        RefreshPanelItems();
        SelectedPanelItem = PanelItems.FirstOrDefault(item => item.Id == itemId);
        StatusMessage = $"Item order updated in panel '{SelectedPanel.Name}'.";
        MarkWorkspaceChanged();
        return true;
    }

    public bool MoveItemToPanel(Guid itemId, Guid targetPanelId)
    {
        var sourcePanel = Panels.FirstOrDefault(panel => panel.ItemIds.Contains(itemId));
        var targetPanel = Panels.FirstOrDefault(panel => panel.Id == targetPanelId);
        if (sourcePanel is null || targetPanel is null)
        {
            return false;
        }

        if (sourcePanel.Id == targetPanel.Id)
        {
            SelectedPanel = targetPanel;
            return false;
        }

        sourcePanel.ItemIds.Remove(itemId);
        targetPanel.ItemIds.Add(itemId);
        SelectedPanel = targetPanel;
        SelectedPanelItem = PanelItems.FirstOrDefault(item => item.Id == itemId);
        UpdateSummary();
        StatusMessage = $"Item moved to panel '{targetPanel.Name}'.";
        MarkWorkspaceChanged();
        return true;
    }

    private void OnPanelsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var panel in e.OldItems.OfType<PanelEditorItemViewModel>())
            {
                panel.PropertyChanged -= OnPanelPropertyChanged;
                panel.ItemIds.CollectionChanged -= OnPanelItemIdsCollectionChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var panel in e.NewItems.OfType<PanelEditorItemViewModel>())
            {
                panel.PropertyChanged += OnPanelPropertyChanged;
                panel.ItemIds.CollectionChanged += OnPanelItemIdsCollectionChanged;
            }
        }

        MarkWorkspaceChanged();
    }

    private void OnGroupsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var group in e.OldItems.OfType<GroupEditorItemViewModel>())
            {
                group.PropertyChanged -= OnGroupPropertyChanged;
                group.ItemIds.CollectionChanged -= OnGroupItemIdsCollectionChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var group in e.NewItems.OfType<GroupEditorItemViewModel>())
            {
                group.PropertyChanged += OnGroupPropertyChanged;
                group.ItemIds.CollectionChanged += OnGroupItemIdsCollectionChanged;
            }
        }

        MarkWorkspaceChanged();
    }

    private void OnLaunchProfilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var profile in e.OldItems.OfType<LaunchProfileEditorItemViewModel>())
            {
                profile.PropertyChanged -= OnLaunchProfilePropertyChanged;
                profile.Steps.CollectionChanged -= OnLaunchProfileStepsCollectionChanged;
                foreach (var step in profile.Steps)
                {
                    step.PropertyChanged -= OnLaunchProfileStepPropertyChanged;
                }
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var profile in e.NewItems.OfType<LaunchProfileEditorItemViewModel>())
            {
                profile.PropertyChanged += OnLaunchProfilePropertyChanged;
                profile.Steps.CollectionChanged += OnLaunchProfileStepsCollectionChanged;
                foreach (var step in profile.Steps)
                {
                    step.PropertyChanged += OnLaunchProfileStepPropertyChanged;
                }
            }
        }

        MarkWorkspaceChanged();
    }

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MarkWorkspaceChanged();
    }

    private void OnPanelItemIdsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshGroupSourcePanelItems();
        RefreshLaunchProfileSourcePanelItems();
        MarkWorkspaceChanged();
    }

    private void OnGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MarkWorkspaceChanged();
    }

    private void OnGroupItemIdsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        MarkWorkspaceChanged();
    }

    private void OnLaunchProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MarkWorkspaceChanged();
    }

    private void OnLaunchProfileStepsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var step in e.OldItems.OfType<LaunchProfileStepEditorItemViewModel>())
            {
                step.PropertyChanged -= OnLaunchProfileStepPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var step in e.NewItems.OfType<LaunchProfileStepEditorItemViewModel>())
            {
                step.PropertyChanged += OnLaunchProfileStepPropertyChanged;
            }
        }

        MarkWorkspaceChanged();
    }

    private void OnLaunchProfileStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MarkWorkspaceChanged();
    }

    private void OnLauncherItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MarkWorkspaceChanged();
    }

    private void MarkWorkspaceChanged()
    {
        if (_suppressChangeTracking || !_isLoaded)
        {
            return;
        }

        HasUnsavedChanges = true;
        SchedulePreviewRefresh();
    }

    private void SchedulePreviewRefresh()
    {
        _previewCancellationTokenSource?.Cancel();
        _previewCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _previewCancellationTokenSource.Token;
        var requestVersion = ++_previewRequestVersion;
        _ = PreviewWorkspaceAsync(requestVersion, cancellationToken);
    }

    private async Task PreviewWorkspaceAsync(int requestVersion, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(180, cancellationToken);
            if (cancellationToken.IsCancellationRequested || requestVersion != _previewRequestVersion)
            {
                return;
            }

            await _dockShellController.PreviewWorkspaceAsync(BuildWorkspace(), cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private Workspace BuildWorkspace()
    {
        var panels = Panels.Select(panelVm =>
        {
            var panel = new Panel(
                panelVm.Id,
                panelVm.Name,
                panelVm.Position,
                panelVm.LayoutMode,
                new PanelAppearance(
                    Math.Clamp(panelVm.Opacity, 0.35, 1),
                    Math.Clamp(panelVm.IconSize, 24, 96),
                    panelVm.AlwaysOnTop,
                    panelVm.LabelDisplayMode == PanelLabelDisplayMode.AlwaysVisible,
                    panelVm.AutoHide,
                    panelVm.Locked,
                    panelVm.FloatingLeft,
                    panelVm.FloatingTop,
                    panelVm.LabelDisplayMode,
                    panelVm.LabelPlacement,
                    panelVm.IconShape,
                    panelVm.HorizontalPadding,
                    panelVm.VerticalPadding,
                    panelVm.LabelSpacing,
                    panelVm.TextSize,
                    panelVm.PanelColor,
                    panelVm.Orientation,
                    panelVm.DockOffset,
                    panelVm.FlyoutDisplayMode,
                    panelVm.PersistedGroupOpenMode,
                    false,
                    false,
                    PanelCollapseButtonSide.Right,
                    false,
                    5,
                    panelVm.OverflowMode,
                    Math.Clamp(panelVm.MaxOverflowTracks, 1, 6),
                    panelVm.IsHidden,
                    panelVm.CustomWidth,
                    panelVm.CustomHeight));

            foreach (var itemId in panelVm.ItemIds)
            {
                panel.AddItem(itemId);
            }

            return panel;
        }).ToArray();

        var assignedItemIds = panels
            .SelectMany(panel => panel.ItemIds)
            .Distinct()
            .ToHashSet();

        foreach (var group in Groups)
        {
            foreach (var itemId in group.ItemIds)
            {
                assignedItemIds.Add(itemId);
            }
        }

        foreach (var profile in LaunchProfiles)
        {
            foreach (var step in profile.Steps)
            {
                assignedItemIds.Add(step.ItemId);
            }
        }

        var items = _allItems
            .Where(item => assignedItemIds.Contains(item.Id))
            .Select(itemVm => new LauncherItem(
                itemVm.Id,
                itemVm.DisplayName,
                itemVm.Type,
                itemVm.Target,
                itemVm.Arguments,
                itemVm.RunAsAdministrator,
                itemVm.IconPath))
            .ToArray();

        var groups = Groups.Select(groupVm =>
        {
            var group = new Group(groupVm.Id, groupVm.Name);
            foreach (var itemId in groupVm.ItemIds)
            {
                if (items.Any(item => item.Id == itemId && !IsSpecialLauncherTarget(item.Target)))
                {
                    group.AddItem(itemId);
                }
            }

            return group;
        }).ToArray();

        foreach (var launcher in _allItems.Where(item => IsGroupLauncherTarget(item.Target)))
        {
            if (TryParseGroupTarget(itemTarget: launcher.Target, out var groupId))
            {
                var group = groups.FirstOrDefault(candidate => candidate.Id == groupId);
                if (group is not null)
                {
                    launcher.DisplayName = group.Name;
                }
            }
        }

        var launchProfiles = LaunchProfiles.Select(profileVm =>
            new LaunchProfile(
                profileVm.Id,
                profileVm.Name,
                profileVm.Steps
                    .Where(step => items.Any(item => item.Id == step.ItemId && !IsSpecialLauncherTarget(item.Target)))
                    .Select(step => new LaunchStep(step.ItemId, Math.Max(step.DelayMs, 0), step.RunAsAdministrator))
                    .ToArray()))
            .ToArray();

        foreach (var launcher in _allItems.Where(item => IsLaunchProfileLauncherTarget(item.Target)))
        {
            if (TryParseSpecialTarget(launcher.Target, "profile:", out var profileId))
            {
                var profile = launchProfiles.FirstOrDefault(candidate => candidate.Id == profileId);
                if (profile is not null)
                {
                    launcher.DisplayName = profile.Name;
                }
            }
        }

        return new Workspace(
            1,
            new AppSettings("en", "system", false, "Alt+Space"),
            panels,
            items,
            groups,
            launchProfiles);
    }

    private void SetItemsFromWorkspace(IReadOnlyList<LauncherItem> items)
    {
        foreach (var existingItem in _allItems)
        {
            existingItem.PropertyChanged -= OnLauncherItemPropertyChanged;
        }

        _allItems.Clear();
        foreach (var item in items)
        {
            var viewModel = new LauncherItemEditorItemViewModel(
                item.Id,
                item.DisplayName,
                item.Type,
                item.Target,
                item.Arguments,
                item.RunAsAdministrator,
                item.IconPath,
                _iconProvider.GetIcon(item.Type, item.Target, item.IconPath));
            viewModel.PropertyChanged += OnLauncherItemPropertyChanged;
            _allItems.Add(viewModel);
        }

        RefreshPanelItems();
        RefreshGroupItems();
        RefreshLaunchProfileSteps();
    }

    private void RefreshPanelItems()
    {
        PanelItems.Clear();

        if (SelectedPanel is null)
        {
            return;
        }

        var availableItems = _allItems.ToDictionary(item => item.Id);
        foreach (var itemId in SelectedPanel.ItemIds)
        {
            if (availableItems.TryGetValue(itemId, out var item))
            {
                PanelItems.Add(item);
            }
        }

        SelectedPanelItem = PanelItems.FirstOrDefault();
        if (SelectedMoveTargetPanel?.Id == SelectedPanel?.Id || SelectedMoveTargetPanel is null || Panels.All(panel => panel.Id != SelectedMoveTargetPanel.Id))
        {
            SelectedMoveTargetPanel = Panels.FirstOrDefault(panel => panel.Id != SelectedPanel?.Id);
        }

        MoveSelectedItemToPanelCommand.NotifyCanExecuteChanged();
    }

    private void UpdateSummary()
    {
        Summary = $"Panels: {Panels.Count} | Items in selected panel: {PanelItems.Count} | Groups: {Groups.Count} | Profiles: {LaunchProfiles.Count}";
    }

    private void UpdateSummary(Workspace workspace)
    {
        Summary = $"{workspace.Settings.Language} | {workspace.Settings.Theme} | Panels: {workspace.Panels.Count} | Items: {workspace.Items.Count} | Groups: {workspace.Groups.Count} | Profiles: {workspace.LaunchProfiles.Count}";
    }

    private void ApplyDraftFromTarget(string target)
    {
        var draft = LauncherItemDraftFactory.Create(target);
        DraftItemTarget = draft.Target;
        DraftItemType = draft.Type;
        if (string.IsNullOrWhiteSpace(DraftItemName) || DraftItemName == DefaultItemName)
        {
            DraftItemName = draft.DisplayName;
        }
    }

    private LauncherItemEditorItemViewModel CreateDraftItem(string target, string? displayNameOverride = null, LauncherItemType? itemTypeOverride = null)
    {
        var draft = LauncherItemDraftFactory.Create(target);
        var displayName = string.IsNullOrWhiteSpace(displayNameOverride) || displayNameOverride == DefaultItemName
            ? draft.DisplayName
            : displayNameOverride.Trim();
        var itemType = itemTypeOverride ?? draft.Type;
        return new LauncherItemEditorItemViewModel(
            Guid.NewGuid(),
            displayName,
            itemType,
            draft.Target,
            draft.Arguments,
            false,
            null,
            _iconProvider.GetIcon(itemType, draft.Target));
    }

    private void AddItemToPanel(PanelEditorItemViewModel panel, LauncherItemEditorItemViewModel item)
    {
        AddWorkspaceItem(item);
        panel.ItemIds.Add(item.Id);
        RefreshPanelItems();
        UpdateSummary();
        RemoveSelectedItemCommand.NotifyCanExecuteChanged();
        LaunchSelectedItemCommand.NotifyCanExecuteChanged();
        MoveSelectedItemToPanelCommand.NotifyCanExecuteChanged();
    }

    private void ResetDraftItem()
    {
        DraftItemName = DefaultItemName;
        DraftItemTarget = DefaultItemTarget;
        DraftItemType = LauncherItemType.Application;
        SelectedBuiltInAction = BuiltInActions.FirstOrDefault();
    }

    private void AddWorkspaceItem(LauncherItemEditorItemViewModel item)
    {
        item.PropertyChanged += OnLauncherItemPropertyChanged;
        _allItems.Add(item);
        RefreshGroupSourcePanelItems();
        RefreshLaunchProfileSourcePanelItems();
    }

    private void RefreshGroupItems()
    {
        GroupItems.Clear();
        if (SelectedGroup is null)
        {
            return;
        }

        var itemLookup = _allItems
            .Where(item => !IsSpecialLauncherTarget(item.Target))
            .ToDictionary(item => item.Id);

        foreach (var itemId in SelectedGroup.ItemIds)
        {
            if (itemLookup.TryGetValue(itemId, out var item))
            {
                GroupItems.Add(item);
            }
        }

        SelectedGroupItem = GroupItems.FirstOrDefault();

        AddSelectedItemToGroupCommand.NotifyCanExecuteChanged();
        RemoveSelectedItemFromGroupCommand.NotifyCanExecuteChanged();
        AddSelectedGroupToPanelCommand.NotifyCanExecuteChanged();
    }

    private void RefreshGroupSourcePanelItems()
    {
        GroupSourcePanelItems.Clear();
        if (SelectedGroupSourcePanel is null)
        {
            return;
        }

        var availableItems = _allItems
            .Where(item => !IsSpecialLauncherTarget(item.Target))
            .ToDictionary(item => item.Id);

        foreach (var itemId in SelectedGroupSourcePanel.ItemIds)
        {
            if (availableItems.TryGetValue(itemId, out var item))
            {
                GroupSourcePanelItems.Add(item);
            }
        }

        SelectedGroupSourceItem = GroupSourcePanelItems.FirstOrDefault();
    }

    private void SelectSourcePanelForEditing(PanelEditorItemViewModel? sourcePanel)
    {
        if (sourcePanel is not null && SelectedPanel?.Id != sourcePanel.Id)
        {
            SelectedPanel = sourcePanel;
        }
    }

    private void ApplyBuiltInActionDraft(BuiltInDockActionPreset preset)
    {
        DraftItemName = preset.DisplayName;
        DraftItemTarget = preset.Target;
        DraftItemType = LauncherItemType.Action;
    }

    private static string BuildGroupTarget(Guid groupId)
    {
        return $"group:{groupId:D}";
    }

    private static bool IsGroupLauncherTarget(string target)
    {
        return target.StartsWith("group:", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildLaunchProfileTarget(Guid profileId)
    {
        return $"profile:{profileId:D}";
    }

    private static bool IsLaunchProfileLauncherTarget(string target)
    {
        return target.StartsWith("profile:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSpecialLauncherTarget(string target)
    {
        return IsGroupLauncherTarget(target) || IsLaunchProfileLauncherTarget(target);
    }

    private static bool TryParseGroupTarget(string itemTarget, out Guid groupId)
    {
        return TryParseSpecialTarget(itemTarget, "group:", out groupId);
    }

    private static bool TryParseSpecialTarget(string target, string prefix, out Guid id)
    {
        if (target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(target[prefix.Length..], out id))
        {
            return true;
        }

        id = Guid.Empty;
        return false;
    }

    private void RefreshLaunchProfileSteps()
    {
        SelectedLaunchProfileSteps.Clear();
        if (SelectedLaunchProfile is null)
        {
            return;
        }

        foreach (var step in SelectedLaunchProfile.Steps)
        {
            step.ItemDisplayName = _allItems.FirstOrDefault(item => item.Id == step.ItemId)?.DisplayName ?? "Missing Item";
            SelectedLaunchProfileSteps.Add(step);
        }

        SelectedLaunchProfileStep = SelectedLaunchProfileSteps.FirstOrDefault();
        RemoveSelectedLaunchProfileCommand.NotifyCanExecuteChanged();
        AddSelectedItemToLaunchProfileCommand.NotifyCanExecuteChanged();
        RemoveSelectedStepFromLaunchProfileCommand.NotifyCanExecuteChanged();
        AddSelectedLaunchProfileToPanelCommand.NotifyCanExecuteChanged();
    }

    private void RefreshLaunchProfileSourcePanelItems()
    {
        LaunchProfileSourcePanelItems.Clear();
        if (SelectedLaunchProfileSourcePanel is null)
        {
            return;
        }

        var availableItems = _allItems
            .Where(item => !IsSpecialLauncherTarget(item.Target))
            .ToDictionary(item => item.Id);

        foreach (var itemId in SelectedLaunchProfileSourcePanel.ItemIds)
        {
            if (availableItems.TryGetValue(itemId, out var item))
            {
                LaunchProfileSourcePanelItems.Add(item);
            }
        }

        SelectedLaunchProfileSourceItem = LaunchProfileSourcePanelItems.FirstOrDefault();
    }
}

public sealed partial class PanelEditorItemViewModel : ObservableObject
{
    public PanelEditorItemViewModel(
        Guid id,
        string name,
        PanelPosition position,
        PanelLayoutMode layoutMode,
        double opacity,
        int iconSize,
        bool alwaysOnTop,
        PanelLabelDisplayMode labelDisplayMode,
        PanelLabelPlacement labelPlacement,
        PanelIconShape iconShape,
        PanelOrientation? orientation,
        double horizontalPadding,
        double verticalPadding,
        double labelSpacing,
        double textSize,
        string panelColor,
        PanelFlyoutDisplayMode? flyoutDisplayMode,
        PanelGroupOpenMode? groupOpenMode,
        bool autoHide,
        bool locked,
        double? floatingLeft,
        double? floatingTop,
        double? dockOffset,
        PanelOverflowMode overflowMode,
        int maxOverflowTracks,
        bool isHidden,
        double? customWidth,
        double? customHeight)
    {
        Id = id;
        this.name = name;
        this.position = position;
        this.layoutMode = layoutMode;
        this.opacity = opacity;
        this.iconSize = iconSize;
        this.alwaysOnTop = alwaysOnTop;
        this.labelDisplayMode = labelDisplayMode;
        this.labelPlacement = labelPlacement;
        this.iconShape = iconShape;
        this.orientation = orientation;
        this.horizontalPadding = horizontalPadding;
        this.verticalPadding = verticalPadding;
        this.labelSpacing = labelSpacing;
        this.textSize = textSize;
        this.panelColor = panelColor;
        PersistedGroupOpenMode = groupOpenMode;
        this.flyoutDisplayMode = flyoutDisplayMode ?? PanelFlyoutDisplayMode.Tiles;
        this.groupOpenMode = groupOpenMode ?? PanelGroupOpenMode.Floating;
        this.autoHide = position == PanelPosition.Floating ? false : autoHide;
        this.@locked = locked;
        this.floatingLeft = floatingLeft;
        this.floatingTop = floatingTop;
        this.dockOffset = dockOffset;
        this.overflowMode = overflowMode;
        this.maxOverflowTracks = Math.Clamp(maxOverflowTracks, 1, 6);
        this.isHidden = isHidden;
        this.customWidth = customWidth;
        this.customHeight = customHeight;
    }

    public Guid Id { get; }

    public ObservableCollection<Guid> ItemIds { get; } = [];

    public PanelGroupOpenMode? PersistedGroupOpenMode { get; private set; }

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private PanelPosition position;

    [ObservableProperty]
    private PanelLayoutMode layoutMode;

    [ObservableProperty]
    private double opacity;

    [ObservableProperty]
    private int iconSize;

    [ObservableProperty]
    private bool alwaysOnTop;

    [ObservableProperty]
    private PanelLabelDisplayMode labelDisplayMode;

    [ObservableProperty]
    private PanelLabelPlacement labelPlacement;

    [ObservableProperty]
    private PanelIconShape iconShape;

    [ObservableProperty]
    private PanelOrientation? orientation;

    [ObservableProperty]
    private double horizontalPadding;

    [ObservableProperty]
    private double verticalPadding;

    [ObservableProperty]
    private double labelSpacing;

    [ObservableProperty]
    private double textSize;

    [ObservableProperty]
    private string panelColor;

    [ObservableProperty]
    private PanelFlyoutDisplayMode flyoutDisplayMode;

    [ObservableProperty]
    private PanelGroupOpenMode groupOpenMode;

    partial void OnGroupOpenModeChanged(PanelGroupOpenMode value)
    {
        PersistedGroupOpenMode = value;
    }

    [ObservableProperty]
    private bool autoHide;

    [ObservableProperty]
    private bool @locked;

    [ObservableProperty]
    private double? floatingLeft;

    [ObservableProperty]
    private double? floatingTop;

    [ObservableProperty]
    private double? dockOffset;

    [ObservableProperty]
    private PanelOverflowMode overflowMode;

    [ObservableProperty]
    private int maxOverflowTracks;

    [ObservableProperty]
    private bool isHidden;

    [ObservableProperty]
    private double? customWidth;

    [ObservableProperty]
    private double? customHeight;

    public bool IsFloating => Position == PanelPosition.Floating;

    public bool IsDocked => Position != PanelPosition.Floating;

    public bool CanAutoHide => IsDocked;

    public string AutoHideTooltip => CanAutoHide
        ? "Hide this panel until the cursor reaches its screen edge."
        : "Auto Hide is available only for panels docked to a screen edge.";

    public string DockOffsetLabel => Position is PanelPosition.Left or PanelPosition.Right
        ? "Dock Offset From Top"
        : "Dock Offset From Left";

    public bool LabelsVisible => LabelDisplayMode == PanelLabelDisplayMode.AlwaysVisible;

    public Brush PanelColorBrush => CreateBrush(PanelColor);

    partial void OnLabelDisplayModeChanged(PanelLabelDisplayMode value)
    {
        OnPropertyChanged(nameof(LabelsVisible));
    }

    partial void OnPanelColorChanged(string value)
    {
        OnPropertyChanged(nameof(PanelColorBrush));
    }

    partial void OnPositionChanged(PanelPosition value)
    {
        if (value == PanelPosition.Floating)
        {
            AutoHide = false;
        }
        else
        {
            DockOffset = null;
        }

        OnPropertyChanged(nameof(IsFloating));
        OnPropertyChanged(nameof(IsDocked));
        OnPropertyChanged(nameof(CanAutoHide));
        OnPropertyChanged(nameof(AutoHideTooltip));
        OnPropertyChanged(nameof(DockOffsetLabel));
    }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Name) ? "Unnamed panel" : Name;
    }

    private static Brush CreateBrush(string color)
    {
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(color)!;
        }
        catch
        {
            return Brushes.Transparent;
        }
    }
}

public sealed partial class LauncherItemEditorItemViewModel : ObservableObject
{
    public LauncherItemEditorItemViewModel(
        Guid id,
        string displayName,
        LauncherItemType type,
        string target,
        string? arguments,
        bool runAsAdministrator,
        string? iconPath,
        ImageSource? iconSource)
    {
        Id = id;
        this.displayName = displayName;
        this.type = type;
        this.target = target;
        this.arguments = arguments;
        this.runAsAdministrator = runAsAdministrator;
        this.iconPath = iconPath;
        IconSource = iconSource;
    }

    public Guid Id { get; }

    public ImageSource? IconSource { get; }

    public Visibility IconVisibility => IconSource is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility GlyphVisibility => IconSource is null ? Visibility.Visible : Visibility.Collapsed;

    public string TypeLabel => Type.ToString();

    public string DisplayPath => Target;

    public string TooltipText => $"{DisplayName}\n{TypeLabel}\n{Target}";

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

    [ObservableProperty]
    private string displayName;

    [ObservableProperty]
    private LauncherItemType type;

    [ObservableProperty]
    private string target;

    [ObservableProperty]
    private string? arguments;

    [ObservableProperty]
    private bool runAsAdministrator;

    [ObservableProperty]
    private string? iconPath;

    private static Brush CreateBrush(string color)
    {
        return (Brush)new BrushConverter().ConvertFromString(color)!;
    }
}

public sealed partial class GroupEditorItemViewModel : ObservableObject
{
    public GroupEditorItemViewModel(Guid id, string name)
    {
        Id = id;
        this.name = name;
    }

    public Guid Id { get; }

    public ObservableCollection<Guid> ItemIds { get; } = [];

    [ObservableProperty]
    private string name;
}

public sealed partial class LaunchProfileEditorItemViewModel : ObservableObject
{
    public LaunchProfileEditorItemViewModel(Guid id, string name)
    {
        Id = id;
        this.name = name;
    }

    public Guid Id { get; }

    public ObservableCollection<LaunchProfileStepEditorItemViewModel> Steps { get; } = [];

    [ObservableProperty]
    private string name;
}

public sealed partial class LaunchProfileStepEditorItemViewModel : ObservableObject
{
    public LaunchProfileStepEditorItemViewModel(Guid itemId, int delayMs, bool runAsAdministrator)
    {
        ItemId = itemId;
        itemDisplayName = "Unknown Item";
        this.delayMs = delayMs;
        this.runAsAdministrator = runAsAdministrator;
    }

    public Guid ItemId { get; }

    [ObservableProperty]
    private string itemDisplayName;

    [ObservableProperty]
    private int delayMs;

    [ObservableProperty]
    private bool runAsAdministrator;
}
