using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using DockLauncher.BuildingBlocks.Application.Contracts;
using DockLauncher.AppHost.Dialogs;
using DockLauncher.AppHost.Hotkeys;
using DockLauncher.Modules.Groups.Domain;
using DockLauncher.Modules.Items.Application;
using DockLauncher.Modules.Items.Domain;
using DockLauncher.Modules.LaunchProfiles.Application;
using DockLauncher.Modules.LaunchProfiles.Domain;
using DockLauncher.Modules.Settings.Application;
using DockLauncher.Modules.Settings.Domain;
using DockLauncher.Modules.Settings.Presentation.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.AppHost.Docking;

public sealed class DockShellCoordinator : IDockShellController
{
    private static readonly string[] PinnedShortcutDirectories =
    [
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Internet Explorer",
            "Quick Launch",
            "User Pinned",
            "StartMenu"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Internet Explorer",
            "Quick Launch",
            "User Pinned",
            "TaskBar")
    ];

    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IDockPanelIconProvider _iconProvider;
    private readonly IItemTargetPicker _itemTargetPicker;
    private readonly ITextPromptService _textPromptService;
    private readonly IItemEditorService _itemEditorService;
    private readonly LaunchItemCommandHandler _launchItemCommandHandler;
    private readonly RunLaunchProfileCommandHandler _runLaunchProfileCommandHandler;
    private readonly DockGlobalHotkey _dockGlobalHotkey;
    private readonly Dictionary<Guid, DockPanelWindow> _panelWindows = [];
    private readonly Dictionary<Guid, GroupFlyoutWindow> _groupWindows = [];
    private readonly Dictionary<string, FolderFlyoutWindow> _folderWindows = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Guid> _hiddenPanelIds = [];
    private readonly DispatcherTimer _foregroundWindowTimer;
    private readonly DispatcherTimer _flyoutDismissTimer;
    private bool _leftMouseWasDown;
    private bool _panelsVisible = true;

    public DockShellCoordinator(
        IServiceProvider serviceProvider,
        IWorkspaceStore workspaceStore,
        IDockPanelIconProvider iconProvider,
        IItemTargetPicker itemTargetPicker,
        ITextPromptService textPromptService,
        IItemEditorService itemEditorService,
        LaunchItemCommandHandler launchItemCommandHandler,
        RunLaunchProfileCommandHandler runLaunchProfileCommandHandler,
        DockGlobalHotkey dockGlobalHotkey)
    {
        _serviceProvider = serviceProvider;
        _workspaceStore = workspaceStore;
        _iconProvider = iconProvider;
        _itemTargetPicker = itemTargetPicker;
        _textPromptService = textPromptService;
        _itemEditorService = itemEditorService;
        _launchItemCommandHandler = launchItemCommandHandler;
        _runLaunchProfileCommandHandler = runLaunchProfileCommandHandler;
        _dockGlobalHotkey = dockGlobalHotkey;

        if (Application.Current is not null)
        {
            Application.Current.Deactivated += (_, _) => CloseFlyoutWindows();
        }

        _foregroundWindowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _foregroundWindowTimer.Tick += (_, _) => EnsurePanelWindowsVisible();
        _foregroundWindowTimer.Start();

        _flyoutDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
        _flyoutDismissTimer.Tick += (_, _) => DismissFlyoutsOnOutsideClick();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        await ApplyWorkspaceAsync(workspace, refreshHotkeyRegistration: true, cancellationToken);
    }

    public Task PreviewWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        return ApplyWorkspaceAsync(workspace, refreshHotkeyRegistration: false, cancellationToken);
    }

    private async Task ApplyWorkspaceAsync(
        Workspace workspace,
        bool refreshHotkeyRegistration,
        CancellationToken cancellationToken)
    {
        if (refreshHotkeyRegistration)
        {
            await _dockGlobalHotkey.RefreshRegistrationAsync(cancellationToken);
        }

        var workspacePanelIds = workspace.Panels.Select(panel => panel.Id).ToHashSet();
        _hiddenPanelIds.RemoveWhere(panelId => !workspacePanelIds.Contains(panelId));
        foreach (var panel in workspace.Panels)
        {
            if (panel.Appearance.IsHidden)
            {
                _hiddenPanelIds.Add(panel.Id);
            }
            else
            {
                _hiddenPanelIds.Remove(panel.Id);
            }
        }

        var panels = workspace.Panels.Select(panel => BuildDockPanelViewModel(panel, workspace)).ToArray();
        var currentPanelIds = panels.Select(panel => panel.PanelId).ToHashSet();

        foreach (var panelId in _panelWindows.Keys.Where(panelId => !currentPanelIds.Contains(panelId)).ToArray())
        {
            _panelWindows[panelId].Close();
            _panelWindows.Remove(panelId);
            _hiddenPanelIds.Remove(panelId);
        }

        foreach (var window in _groupWindows.Values)
        {
            window.CloseImmediately();
        }

        _groupWindows.Clear();

        foreach (var window in _folderWindows.Values)
        {
            window.Close();
        }

        _folderWindows.Clear();

        foreach (var panelViewModel in panels)
        {
            if (_panelWindows.TryGetValue(panelViewModel.PanelId, out var existingWindow))
            {
                existingWindow.UpdateViewModel(panelViewModel);
                SyncPanelWindowVisibility(existingWindow, panelViewModel.PanelId);
                continue;
            }

            var window = new DockPanelWindow(panelViewModel);
            _panelWindows.Add(panelViewModel.PanelId, window);
            SyncPanelWindowVisibility(window, panelViewModel.PanelId);
        }
    }

    private void SyncPanelWindowVisibility(DockPanelWindow window, Guid panelId)
    {
        var shouldBeVisible = IsPanelExpectedVisible(panelId);
        if (shouldBeVisible)
        {
            EnsurePanelWindowVisible(window);
            return;
        }

        if (window.IsVisible)
        {
            window.Hide();
        }
    }

    private bool IsPanelExpectedVisible(Guid panelId)
    {
        return _panelsVisible && !_hiddenPanelIds.Contains(panelId);
    }

    private static void EnsurePanelWindowVisible(DockPanelWindow window)
    {
        if (!window.IsVisible)
        {
            window.Show();
        }

        window.EnsureRuntimeVisible();
    }

    private WorkspaceEditorViewModel? GetVisibleConfiguratorWorkspace()
    {
        return Application.Current.Windows
            .OfType<MainWindow>()
            .Where(window => window.IsVisible)
            .Select(window => (window.DataContext as MainWindowViewModel)?.Workspace)
            .FirstOrDefault(workspace => workspace is not null);
    }

    private async Task<Workspace> LoadRuntimeWorkspaceAsync(CancellationToken cancellationToken)
    {
        return GetVisibleConfiguratorWorkspace()?.CreateWorkspaceSnapshot()
            ?? await _workspaceStore.LoadAsync(cancellationToken);
    }

    private async Task CommitRuntimeWorkspaceAsync(
        Workspace workspace,
        string statusMessage,
        CancellationToken cancellationToken,
        Guid? selectedPanelId = null)
    {
        var configuratorWorkspace = GetVisibleConfiguratorWorkspace();
        if (configuratorWorkspace is not null)
        {
            await configuratorWorkspace.ApplyRuntimeWorkspaceAsync(workspace, statusMessage, cancellationToken, selectedPanelId);
            return;
        }

        await _workspaceStore.SaveAsync(workspace, cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    public async Task AddPathsToPanelAsync(Guid panelId, IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var panel = workspace.Panels.FirstOrDefault(candidate => candidate.Id == panelId);
        if (panel is null)
        {
            return;
        }

        var items = workspace.Items.ToList();
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var draft = LauncherItemDraftFactory.Create(path);
            var item = new LauncherItem(Guid.NewGuid(), draft.DisplayName, draft.Type, draft.Target, draft.Arguments);
            items.Add(item);
            panel.AddItem(item.Id);
        }

        var updatedWorkspace = new Workspace(workspace.SchemaVersion, workspace.Settings, workspace.Panels, items, workspace.Groups, workspace.LaunchProfiles);
        await CommitRuntimeWorkspaceAsync(updatedWorkspace, $"Added {paths.Count} item(s) to panel '{panel.Name}'.", cancellationToken);
    }

    public async Task UpdatePanelPositionAsync(
        Guid panelId,
        Modules.Panels.Domain.PanelPosition position,
        double? floatingLeft = null,
        double? floatingTop = null,
        double? customWidth = null,
        double? customHeight = null,
        CancellationToken cancellationToken = default)
    {
        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var updatedPanels = workspace.Panels
            .Select(panel => panel.Id == panelId
                ? ClonePanelWithPosition(panel, position, floatingLeft, floatingTop, customWidth, customHeight)
                : ClonePanelWithItems(panel, panel.ItemIds))
            .ToArray();

        var updatedWorkspace = new Workspace(workspace.SchemaVersion, workspace.Settings, updatedPanels, workspace.Items, workspace.Groups, workspace.LaunchProfiles);
        await CommitRuntimeWorkspaceAsync(updatedWorkspace, "Panel position updated from runtime panel.", cancellationToken);
    }

    public void ShowConfigurator()
    {
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
        mainWindow.Activate();
    }

    private async Task AddFileToPanelAsync(Guid panelId, CancellationToken cancellationToken = default)
    {
        var path = await _itemTargetPicker.PickFileAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await AddPathsToPanelAsync(panelId, [path], cancellationToken);
    }

    private async Task AddFolderToPanelAsync(Guid panelId, CancellationToken cancellationToken = default)
    {
        var path = await _itemTargetPicker.PickFolderAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await AddPathsToPanelAsync(panelId, [path], cancellationToken);
    }

    private async Task AddSeparatorToPanelAsync(Guid panelId, CancellationToken cancellationToken = default)
    {
        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var panel = workspace.Panels.FirstOrDefault(candidate => candidate.Id == panelId);
        if (panel is null)
        {
            return;
        }

        var separator = new LauncherItem(
            Guid.NewGuid(),
            "Separator",
            LauncherItemType.Separator,
            $"separator:{Guid.NewGuid()}");

        panel.AddItem(separator.Id);
        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            workspace.Panels,
            workspace.Items.Concat([separator]).ToArray(),
            workspace.Groups,
            workspace.LaunchProfiles);

        await CommitRuntimeWorkspaceAsync(updatedWorkspace, $"Separator added to panel '{panel.Name}'.", cancellationToken);
    }

    private async Task ImportPinnedShortcutsToPanelAsync(Guid panelId, CancellationToken cancellationToken = default)
    {
        var shortcutPaths = EnumeratePinnedShortcutPaths().ToArray();
        if (shortcutPaths.Length == 0)
        {
            MessageBox.Show(
                "No pinned shortcut files were found in the known Windows pinned folders.",
                "DockLauncher",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var panel = workspace.Panels.FirstOrDefault(candidate => candidate.Id == panelId);
        if (panel is null)
        {
            return;
        }

        var existingTargets = workspace.Items
            .Select(item => item.Target)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var items = workspace.Items.ToList();
        var addedCount = 0;

        foreach (var shortcutPath in shortcutPaths)
        {
            if (!existingTargets.Add(shortcutPath))
            {
                continue;
            }

            var item = new LauncherItem(
                Guid.NewGuid(),
                Path.GetFileNameWithoutExtension(shortcutPath),
                LauncherItemType.Shortcut,
                shortcutPath);
            items.Add(item);
            panel.AddItem(item.Id);
            addedCount++;
        }

        if (addedCount == 0)
        {
            MessageBox.Show(
                "Pinned shortcuts are already present in this workspace.",
                "DockLauncher",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            workspace.Panels,
            items,
            workspace.Groups,
            workspace.LaunchProfiles);
        await CommitRuntimeWorkspaceAsync(updatedWorkspace, $"Imported {addedCount} pinned shortcut(s) to panel '{panel.Name}'.", cancellationToken);
    }

    private static IEnumerable<string> EnumeratePinnedShortcutPaths()
    {
        foreach (var directory in PinnedShortcutDirectories.Where(Directory.Exists))
        {
            foreach (var shortcutPath in Directory.EnumerateFiles(directory, "*.lnk", SearchOption.TopDirectoryOnly))
            {
                yield return shortcutPath;
            }
        }
    }

    private Task OpenGroupsEditorAsync()
    {
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
        mainWindow.Activate();

        if (mainWindow.DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Workspace.AddGroupCommand.Execute(null);
            viewModel.ShowGroupsEditor();
        }

        return Task.CompletedTask;
    }

    private Task OpenLaunchProfilesEditorAsync()
    {
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
        mainWindow.Activate();

        if (mainWindow.DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Workspace.AddLaunchProfileCommand.Execute(null);
            viewModel.ShowLaunchProfilesEditor();
        }

        return Task.CompletedTask;
    }

    public void TogglePanelsVisibility()
    {
        _panelsVisible = !_panelsVisible;
        if (!_panelsVisible)
        {
            foreach (var window in _groupWindows.Values)
            {
                window.CloseImmediately();
            }

            _groupWindows.Clear();

            foreach (var window in _folderWindows.Values)
            {
                window.Close();
            }

            _folderWindows.Clear();
        }

        EnsurePanelWindowsVisible();
    }

    public void EnsurePanelsVisible()
    {
        _panelsVisible = true;
        EnsurePanelWindowsVisible();
    }

    public async void ShowHiddenPanels()
    {
        try
        {
            var workspace = await LoadRuntimeWorkspaceAsync(CancellationToken.None);
            var updatedPanels = workspace.Panels
                .Select(panel => panel.Appearance.IsHidden
                    ? ClonePanel(panel, panel.Name, panel.Position, panel.LayoutMode, panel.Appearance with { IsHidden = false }, panel.ItemIds)
                    : ClonePanelWithItems(panel, panel.ItemIds))
                .ToArray();

            var updatedWorkspace = new Workspace(
                workspace.SchemaVersion,
                workspace.Settings,
                updatedPanels,
                workspace.Items,
                workspace.Groups,
                workspace.LaunchProfiles);

            await CommitRuntimeWorkspaceAsync(updatedWorkspace, "Hidden panels restored.", CancellationToken.None);
        }
        catch
        {
            // The tray command must not tear down the application on persistence failures.
        }

        _hiddenPanelIds.Clear();
        if (!_panelsVisible)
        {
            _panelsVisible = true;
        }

        EnsurePanelWindowsVisible();
    }

    private void EnsurePanelWindowsVisible()
    {
        foreach (var window in _panelWindows.Values.ToArray())
        {
            SyncPanelWindowVisibility(window, ((DockPanelWindowViewModel)window.DataContext).PanelId);
        }
    }

    private static bool IsForegroundWindowFullscreenOutsideDockLauncher()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero || IsDockLauncherWindow(foregroundWindow))
        {
            return false;
        }

        if (!GetWindowRect(foregroundWindow, out var windowRect))
        {
            return false;
        }

        var monitor = MonitorFromWindow(foregroundWindow, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var monitorInfo = MonitorInfo.Create();
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        const int tolerance = 2;
        return windowRect.Left <= monitorInfo.Monitor.Left + tolerance
            && windowRect.Top <= monitorInfo.Monitor.Top + tolerance
            && windowRect.Right >= monitorInfo.Monitor.Right - tolerance
            && windowRect.Bottom >= monitorInfo.Monitor.Bottom - tolerance;
    }

    private static bool IsDockLauncherWindow(IntPtr handle)
    {
        if (Application.Current is null)
        {
            return false;
        }

        return Application.Current.Windows
            .OfType<Window>()
            .Any(window => new WindowInteropHelper(window).Handle == handle);
    }

    public void Exit()
    {
        foreach (var mainWindow in Application.Current.Windows.OfType<MainWindow>())
        {
            mainWindow.AllowClose();
        }

        Application.Current.Shutdown();
    }

    private DockPanelWindowViewModel BuildDockPanelViewModel(Modules.Panels.Domain.Panel panel, Workspace workspace)
    {
        var itemById = workspace.Items.ToDictionary(item => item.Id);
        var items = panel.ItemIds
            .Where(itemById.ContainsKey)
            .Select(itemId => itemById[itemId])
            .Select(item => new DockPanelItemViewModel(
                item.Id,
                item.DisplayName,
                item.Type,
                item.Target,
                item.Arguments,
                item.RunAsAdministrator,
                _iconProvider.GetIcon(item.Type, item.Target, item.IconPath),
                workspace.Panels
                    .Where(candidate => candidate.Id != panel.Id)
                    .Select(candidate => new DockPanelMoveTargetViewModel(candidate.Id, candidate.Name))
                    .ToArray()))
            .ToArray();

        var metrics = CalculateMetrics(panel, items.Length);

        return new DockPanelWindowViewModel(
            panel.Id,
            panel.Name,
            panel.Position,
            metrics.Orientation,
            metrics.Width,
            metrics.Height,
            metrics.ExpandedWidth,
            metrics.ExpandedHeight,
            metrics.CollapsedWidth,
            metrics.CollapsedHeight,
            metrics.Left,
            metrics.Top,
            metrics.HorizontalScrollEnabled,
            metrics.VerticalScrollEnabled,
            panel.Appearance.AlwaysOnTop,
            Math.Clamp(panel.Appearance.Opacity, 0.35, 1),
            Math.Clamp(panel.Appearance.IconSize, 24, 96),
            Math.Clamp(panel.Appearance.HorizontalPadding, 0, 48),
            Math.Clamp(panel.Appearance.VerticalPadding, 0, 48),
            Math.Clamp(panel.Appearance.LabelSpacing, 0, 20),
            Math.Clamp(panel.Appearance.TextSize, 9, 22),
            panel.Appearance.ResolvedPanelColor,
            panel.Appearance.ResolvedLabelDisplayMode,
            panel.Appearance.ResolvedLabelPlacement,
            panel.Appearance.ResolvedIconShape,
            metrics.OverflowMode,
            metrics.MaxOverflowTracks,
            metrics.ActiveOverflowTracks,
            metrics.PrimaryVisibleSlots,
            metrics.OverflowActive,
            panel.Position != Modules.Panels.Domain.PanelPosition.Floating && panel.Appearance.AutoHide,
            panel.Appearance.Locked,
            items,
            item => ActivateDockItemAsync(panel.Id, item, workspace, metrics),
            ShowConfigurator,
            () => RefreshAsync(),
            paths => AddPathsToPanelAsync(panel.Id, paths),
            () => AddFileToPanelAsync(panel.Id),
            () => AddFolderToPanelAsync(panel.Id),
            () => AddSeparatorToPanelAsync(panel.Id),
            () => ImportPinnedShortcutsToPanelAsync(panel.Id),
            () => OpenGroupsEditorAsync(),
            () => OpenLaunchProfilesEditorAsync(),
            (position, floatingLeft, floatingTop, customWidth, customHeight) => UpdatePanelPositionAsync(panel.Id, position, floatingLeft, floatingTop, customWidth, customHeight),
            () => RenamePanelAsync(panel.Id, panel.Name),
            () => DuplicatePanelAsync(panel.Id),
            () => CreateEmptyPanelAsync(panel.Id),
            () => RemovePanelAsync(panel.Id),
            () => HidePanelAsync(panel.Id),
            () => TogglePanelLockAsync(panel.Id),
            () => TogglePanelLabelsAsync(panel.Id),
            () => TogglePanelAlwaysOnTopAsync(panel.Id),
            () => TogglePanelAutoHideAsync(panel.Id),
            orientation => SetPanelOrientationAsync(panel.Id, orientation),
            () => IncreasePanelIconSizeAsync(panel.Id),
            () => DecreasePanelIconSizeAsync(panel.Id),
            OpenItemLocationAsync,
            item => DuplicateItemOnPanelAsync(panel.Id, item),
            item => DuplicateItemToNewPanelAsync(panel.Id, item.Id),
            item => RemoveItemFromPanelAsync(panel.Id, item.Id),
            item => RenameItemAsync(item.Id, item.DisplayName),
            item => EditItemAsync(item.Id),
            (item, target) => MoveItemToPanelAsync(panel.Id, item.Id, target.PanelId),
            (itemId, targetItemId) => MoveItemWithinPanelAsync(panel.Id, itemId, targetItemId),
            HasOpenFlyouts,
            Exit);
    }

    private bool HasOpenFlyouts()
    {
        return _groupWindows.Count > 0 || _folderWindows.Count > 0;
    }

    private static PanelMetrics CalculateMetrics(Modules.Panels.Domain.Panel panel, int itemCount)
    {
        const double edgeMargin = 24d;
        const double chromeAllowance = 6d;
        const double overflowIndicatorGutter = 24d;

        var count = Math.Max(itemCount, 1);
        var workArea = SystemParameters.WorkArea;
        var orientation = ResolvePanelOrientation(panel);
        var panelPaddingHorizontal = Math.Clamp(panel.Appearance.HorizontalPadding, 0d, 48d);
        var panelPaddingVertical = Math.Clamp(panel.Appearance.VerticalPadding, 0d, 48d);
        var horizontalItemGap = ResolveDockItemGap(panelPaddingHorizontal);
        var verticalItemGap = ResolveDockItemGap(panelPaddingVertical);
        const double fixedEdgePadding = 12d;
        var horizontalEdgePadding = fixedEdgePadding;
        var verticalEdgePadding = fixedEdgePadding;
        var effectivePanelPaddingHorizontal = orientation == Orientation.Horizontal ? horizontalEdgePadding : panelPaddingHorizontal;
        var effectivePanelPaddingVertical = orientation == Orientation.Vertical ? verticalEdgePadding : panelPaddingVertical;
        var labelTopMargin = Math.Clamp(panel.Appearance.LabelSpacing, 0d, 20d);
        var primaryLabelHeight = Math.Ceiling(Math.Clamp(panel.Appearance.TextSize, 9d, 22d) + 7d);
        var hoverHeadroom = Math.Ceiling(Math.Clamp(panel.Appearance.IconSize * 0.09, 4d, 8d));

        var labelHeight = panel.Appearance.ResolvedLabelDisplayMode == Modules.Panels.Domain.PanelLabelDisplayMode.AlwaysVisible
            ? labelTopMargin + primaryLabelHeight
            : 0d;
        var labelsVisible = panel.Appearance.ResolvedLabelDisplayMode == Modules.Panels.Domain.PanelLabelDisplayMode.AlwaysVisible;
        var itemSlotWidth = labelsVisible
            ? Math.Max(panel.Appearance.IconSize + 30d, 88d)
            : Math.Max(panel.Appearance.IconSize + 18d, 56d);
        var itemSlotHeight = panel.Appearance.IconSize + hoverHeadroom;
        var horizontalItemExtent = itemSlotWidth + horizontalItemGap;
        var verticalItemExtent = itemSlotHeight + labelHeight + verticalItemGap;
        var maxPanelWidth = Math.Max(220d, workArea.Width - (edgeMargin * 2));
        var maxPanelHeight = Math.Max(140d, workArea.Height - (edgeMargin * 2));
        var horizontalPrimarySpace = maxPanelWidth - (effectivePanelPaddingHorizontal * 2) - chromeAllowance;
        var verticalPrimarySpace = maxPanelHeight - (effectivePanelPaddingVertical * 2) - chromeAllowance;
        var maxHorizontalPrimarySlots = Math.Max(1, (int)Math.Floor(horizontalPrimarySpace / horizontalItemExtent));
        var maxVerticalPrimarySlots = Math.Max(1, (int)Math.Floor(verticalPrimarySpace / verticalItemExtent));
        var maxOverflowTracks = panel.Appearance.ResolvedOverflowMode == Modules.Panels.Domain.PanelOverflowMode.ExpandLayout
            ? panel.Appearance.ResolvedMaxOverflowTracks
            : 1;
        var primarySlotLimit = orientation == Orientation.Horizontal ? maxHorizontalPrimarySlots : maxVerticalPrimarySlots;
        var activeOverflowTracks = Math.Clamp((int)Math.Ceiling(count / (double)primarySlotLimit), 1, maxOverflowTracks);
        var totalPrimarySlots = Math.Max(1, (int)Math.Ceiling(count / (double)activeOverflowTracks));
        var primaryVisibleSlots = Math.Clamp(totalPrimarySlots, 1, primarySlotLimit);
        var overflowActive = totalPrimarySlots > primaryVisibleSlots;
        var overflowPrimaryGutter = 0d;

        if (overflowActive)
        {
            overflowPrimaryGutter = overflowIndicatorGutter * 2;
            primarySlotLimit = orientation == Orientation.Horizontal
                ? Math.Max(1, (int)Math.Floor(Math.Max(horizontalItemExtent, horizontalPrimarySpace - overflowPrimaryGutter) / horizontalItemExtent))
                : Math.Max(1, (int)Math.Floor(Math.Max(verticalItemExtent, verticalPrimarySpace - overflowPrimaryGutter) / verticalItemExtent));
            activeOverflowTracks = Math.Clamp((int)Math.Ceiling(count / (double)primarySlotLimit), 1, maxOverflowTracks);
            totalPrimarySlots = Math.Max(1, (int)Math.Ceiling(count / (double)activeOverflowTracks));
            primaryVisibleSlots = Math.Clamp(totalPrimarySlots, 1, primarySlotLimit);
            overflowActive = totalPrimarySlots > primaryVisibleSlots;
            overflowPrimaryGutter = overflowActive ? overflowPrimaryGutter : 0d;
        }

        var expandedWidth = orientation == Orientation.Horizontal
            ? (effectivePanelPaddingHorizontal * 2) + (primaryVisibleSlots * horizontalItemExtent) + overflowPrimaryGutter + chromeAllowance
            : (effectivePanelPaddingHorizontal * 2) + (activeOverflowTracks * horizontalItemExtent) + chromeAllowance;
        var expandedHeight = orientation == Orientation.Horizontal
            ? (effectivePanelPaddingVertical * 2) + (activeOverflowTracks * verticalItemExtent) + chromeAllowance
            : (effectivePanelPaddingVertical * 2) + (primaryVisibleSlots * verticalItemExtent) + overflowPrimaryGutter + chromeAllowance;

        var collapsedWidth = expandedWidth;
        var collapsedHeight = expandedHeight;
        var minWidth = orientation == Orientation.Horizontal
            ? (effectivePanelPaddingHorizontal * 2) + horizontalItemExtent + overflowPrimaryGutter + chromeAllowance
            : (effectivePanelPaddingHorizontal * 2) + horizontalItemExtent + chromeAllowance;
        var minHeight = orientation == Orientation.Horizontal
            ? (effectivePanelPaddingVertical * 2) + verticalItemExtent + chromeAllowance
            : (effectivePanelPaddingVertical * 2) + verticalItemExtent + overflowPrimaryGutter + chromeAllowance;
        var width = Math.Clamp(panel.Appearance.CustomWidth ?? expandedWidth, minWidth, maxPanelWidth);
        var height = Math.Clamp(panel.Appearance.CustomHeight ?? expandedHeight, minHeight, maxPanelHeight);
        if (orientation == Orientation.Horizontal)
        {
            height = Math.Min(height, expandedHeight);
        }
        else
        {
            width = Math.Min(width, expandedWidth);
        }
        var horizontalScrollEnabled = orientation == Orientation.Horizontal && overflowActive;
        var verticalScrollEnabled = orientation == Orientation.Vertical && overflowActive;
        var centeredLeft = workArea.Left + Math.Max(0, (workArea.Width - width) / 2);
        var centeredTop = workArea.Top + Math.Max(0, (workArea.Height - height) / 2);
        var dockLeft = ResolveDockLeft(panel, workArea, width, centeredLeft);
        var dockTop = ResolveDockTop(panel, workArea, height, centeredTop);
        var iconShape = panel.Appearance.ResolvedIconShape;
        var flyoutDisplayMode = panel.Appearance.ResolvedFlyoutDisplayMode;
        var groupOpenMode = panel.Appearance.ResolvedGroupOpenMode;
        var overflowMode = panel.Appearance.ResolvedOverflowMode;

        return panel.Position switch
        {
            Modules.Panels.Domain.PanelPosition.Top => new PanelMetrics(
                orientation,
                width,
                height,
                expandedWidth,
                expandedHeight,
                collapsedWidth,
                collapsedHeight,
                dockLeft,
                workArea.Top + 12,
                Modules.Panels.Domain.PanelPosition.Top,
                horizontalScrollEnabled,
                verticalScrollEnabled,
                iconShape,
                flyoutDisplayMode,
                groupOpenMode,
                overflowMode,
                panel.Appearance.ResolvedMaxOverflowTracks,
                activeOverflowTracks,
                primaryVisibleSlots,
                overflowActive),
            Modules.Panels.Domain.PanelPosition.Bottom => new PanelMetrics(
                orientation,
                width,
                height,
                expandedWidth,
                expandedHeight,
                collapsedWidth,
                collapsedHeight,
                dockLeft,
                workArea.Bottom - height - 12,
                Modules.Panels.Domain.PanelPosition.Bottom,
                horizontalScrollEnabled,
                verticalScrollEnabled,
                iconShape,
                flyoutDisplayMode,
                groupOpenMode,
                overflowMode,
                panel.Appearance.ResolvedMaxOverflowTracks,
                activeOverflowTracks,
                primaryVisibleSlots,
                overflowActive),
            Modules.Panels.Domain.PanelPosition.Left => new PanelMetrics(
                orientation,
                width,
                height,
                expandedWidth,
                expandedHeight,
                collapsedWidth,
                collapsedHeight,
                workArea.Left + 12,
                dockTop,
                Modules.Panels.Domain.PanelPosition.Left,
                horizontalScrollEnabled,
                verticalScrollEnabled,
                iconShape,
                flyoutDisplayMode,
                groupOpenMode,
                overflowMode,
                panel.Appearance.ResolvedMaxOverflowTracks,
                activeOverflowTracks,
                primaryVisibleSlots,
                overflowActive),
            Modules.Panels.Domain.PanelPosition.Right => new PanelMetrics(
                orientation,
                width,
                height,
                expandedWidth,
                expandedHeight,
                collapsedWidth,
                collapsedHeight,
                workArea.Right - width - 12,
                dockTop,
                Modules.Panels.Domain.PanelPosition.Right,
                horizontalScrollEnabled,
                verticalScrollEnabled,
                iconShape,
                flyoutDisplayMode,
                groupOpenMode,
                overflowMode,
                panel.Appearance.ResolvedMaxOverflowTracks,
                activeOverflowTracks,
                primaryVisibleSlots,
                overflowActive),
            _ => new PanelMetrics(
                orientation,
                width,
                height,
                expandedWidth,
                expandedHeight,
                collapsedWidth,
                collapsedHeight,
                panel.Appearance.FloatingLeft ?? (workArea.Left + 48),
                panel.Appearance.FloatingTop ?? (workArea.Top + 48),
                Modules.Panels.Domain.PanelPosition.Floating,
                horizontalScrollEnabled,
                verticalScrollEnabled,
                iconShape,
                flyoutDisplayMode,
                groupOpenMode,
                overflowMode,
                panel.Appearance.ResolvedMaxOverflowTracks,
                activeOverflowTracks,
                primaryVisibleSlots,
                overflowActive)
        };
    }

    internal static Size CalculatePanelWindowSize(Modules.Panels.Domain.Panel panel, int itemCount)
    {
        var metrics = CalculateMetrics(panel, itemCount);
        return new Size(metrics.Width, metrics.Height);
    }

    private static double ResolveDockLeft(Modules.Panels.Domain.Panel panel, Rect workArea, double width, double centeredLeft)
    {
        const double edgeInset = 12d;
        if (panel.Position is not (Modules.Panels.Domain.PanelPosition.Top or Modules.Panels.Domain.PanelPosition.Bottom)
            || panel.Appearance.DockOffset is null)
        {
            return centeredLeft;
        }

        var maxOffset = Math.Max(0, workArea.Width - width - (edgeInset * 2));
        return workArea.Left + edgeInset + Math.Clamp(panel.Appearance.DockOffset.Value, 0, maxOffset);
    }

    private static double ResolveDockTop(Modules.Panels.Domain.Panel panel, Rect workArea, double height, double centeredTop)
    {
        const double edgeInset = 12d;
        if (panel.Position is not (Modules.Panels.Domain.PanelPosition.Left or Modules.Panels.Domain.PanelPosition.Right)
            || panel.Appearance.DockOffset is null)
        {
            return centeredTop;
        }

        var maxOffset = Math.Max(0, workArea.Height - height - (edgeInset * 2));
        return workArea.Top + edgeInset + Math.Clamp(panel.Appearance.DockOffset.Value, 0, maxOffset);
    }

    private static double ResolveDockItemGap(double padding)
    {
        return Math.Clamp(Math.Round(padding * 0.64), 0d, 30d);
    }

    private static Orientation ResolvePanelOrientation(Modules.Panels.Domain.Panel panel)
    {
        if (panel.Appearance.Orientation is not null)
        {
            return panel.Appearance.Orientation == Modules.Panels.Domain.PanelOrientation.Vertical
                ? Orientation.Vertical
                : Orientation.Horizontal;
        }

        return panel.Position is Modules.Panels.Domain.PanelPosition.Left or Modules.Panels.Domain.PanelPosition.Right
            ? Orientation.Vertical
            : Orientation.Horizontal;
    }

    private sealed record PanelMetrics(
        Orientation Orientation,
        double Width,
        double Height,
        double ExpandedWidth,
        double ExpandedHeight,
        double CollapsedWidth,
        double CollapsedHeight,
        double Left,
        double Top,
        Modules.Panels.Domain.PanelPosition Position,
        bool HorizontalScrollEnabled,
        bool VerticalScrollEnabled,
        Modules.Panels.Domain.PanelIconShape IconShape,
        Modules.Panels.Domain.PanelFlyoutDisplayMode FlyoutDisplayMode,
        Modules.Panels.Domain.PanelGroupOpenMode GroupOpenMode,
        Modules.Panels.Domain.PanelOverflowMode OverflowMode,
        int MaxOverflowTracks,
        int ActiveOverflowTracks,
        int PrimaryVisibleSlots,
        bool OverflowActive);

    private async Task ActivateDockItemAsync(Guid sourcePanelId, DockPanelItemViewModel item, Workspace workspace, PanelMetrics panelMetrics)
    {
        if (item.Type == LauncherItemType.Separator)
        {
            return;
        }

        if (TryParseGroupTarget(item.Target, out var groupId))
        {
            ShowGroupFlyout(sourcePanelId, groupId, item.IconSource, workspace, panelMetrics);
            return;
        }

        if (TryParseProfileTarget(item.Target, out var profileId))
        {
            var profile = workspace.LaunchProfiles.FirstOrDefault(candidate => candidate.Id == profileId);
            if (profile is not null)
            {
                await _runLaunchProfileCommandHandler.HandleAsync(new RunLaunchProfileCommand(profile), CancellationToken.None);
            }

            return;
        }

        if (item.Type == LauncherItemType.Folder && Directory.Exists(item.Target))
        {
            ShowFolderFlyout(item.Target, panelMetrics);
            return;
        }

        var launcherItem = new LauncherItem(item.Id, item.DisplayName, item.Type, item.Target, item.Arguments, item.RunAsAdministrator);
        await _launchItemCommandHandler.HandleAsync(new LaunchItemCommand(launcherItem), CancellationToken.None);
    }

    private void ShowGroupFlyout(
        Guid sourcePanelId,
        Guid groupId,
        System.Windows.Media.ImageSource? groupIconSource,
        Workspace workspace,
        PanelMetrics panelMetrics)
    {
        var group = workspace.Groups.FirstOrDefault(candidate => candidate.Id == groupId);
        if (group is null)
        {
            return;
        }

        CloseFlyoutWindows();

        var moveTargets = workspace.Panels
            .Select(panel => new DockPanelMoveTargetViewModel(panel.Id, panel.Name))
            .ToArray();
        var items = group.ItemIds
            .Select(itemId => workspace.Items.FirstOrDefault(item => item.Id == itemId))
            .Where(item => item is not null)
            .Select(item => new GroupFlyoutItemViewModel(
                item!.Id,
                item.DisplayName,
                item.Target,
                _iconProvider.GetIcon(item.Type, item.Target),
                item.Type.ToString(),
                moveTargets))
            .ToArray();

        var flyoutSize = GroupFlyoutWindowViewModel.CalculateWindowSize(
            items.Length,
            panelMetrics.FlyoutDisplayMode,
            panelMetrics.GroupOpenMode);
        var flyoutPoint = ResolveFlyoutPosition(panelMetrics, flyoutSize.Width, flyoutSize.Height);
        var viewModel = new GroupFlyoutWindowViewModel(
            group.Name,
            groupIconSource,
            flyoutPoint.X,
            flyoutPoint.Y,
            panelMetrics.IconShape,
            panelMetrics.FlyoutDisplayMode,
            panelMetrics.GroupOpenMode,
            items,
            async groupItem =>
            {
                var item = workspace.Items.FirstOrDefault(candidate => candidate.Id == groupItem.Id);
                if (item is null)
                {
                    return;
                }

                await _launchItemCommandHandler.HandleAsync(new LaunchItemCommand(item), CancellationToken.None);
            },
            async groupItem =>
            {
                var item = workspace.Items.FirstOrDefault(candidate => candidate.Id == groupItem.Id);
                if (item is null)
                {
                    return;
                }

                var elevatedItem = new LauncherItem(item.Id, item.DisplayName, item.Type, item.Target, item.Arguments, true);
                await _launchItemCommandHandler.HandleAsync(new LaunchItemCommand(elevatedItem), CancellationToken.None);
            },
            OpenGroupItemLocationAsync,
            item => DuplicateItemInGroupAsync(groupId, item.Id),
            item => DuplicateItemToNewPanelAsync(sourcePanelId, item.Id),
            item => RemoveItemFromGroupAsync(groupId, item.Id),
            item => RenameItemAsync(item.Id, item.DisplayName),
            item => EditItemAsync(item.Id),
            (item, target) => MoveItemFromGroupToPanelAsync(groupId, item.Id, target.PanelId));

        var window = new GroupFlyoutWindow(viewModel);
        window.Closed += (_, _) => _groupWindows.Remove(groupId);
        _groupWindows[groupId] = window;
        window.Show();
        StartFlyoutDismissTracking();
    }

    private void ShowFolderFlyout(string path, PanelMetrics panelMetrics)
    {
        CloseFlyoutWindows();

        var flyoutPoint = ResolveFlyoutPosition(panelMetrics, 420, 420);
        var viewModel = new FolderFlyoutWindowViewModel(
            path,
            flyoutPoint.X,
            flyoutPoint.Y,
            panelMetrics.IconShape,
            panelMetrics.FlyoutDisplayMode,
            openPath => OpenFolderFlyoutPathAsync(openPath, path),
            OpenFolderInExplorerAsync,
            OpenFolderEntryLocationAsync);

        var window = new FolderFlyoutWindow(viewModel);
        window.Closed += (_, _) => _folderWindows.Remove(path);
        _folderWindows[path] = window;
        LoadFolderEntries(viewModel, path);
        window.Show();
        StartFlyoutDismissTracking();
    }

    private void CloseFlyoutWindows()
    {
        foreach (var window in _groupWindows.Values.ToArray())
        {
            window.CloseImmediately();
        }

        _groupWindows.Clear();

        foreach (var window in _folderWindows.Values.ToArray())
        {
            window.Close();
        }

        _folderWindows.Clear();
        StopFlyoutDismissTracking();
    }

    private void StartFlyoutDismissTracking()
    {
        _leftMouseWasDown = Mouse.LeftButton == MouseButtonState.Pressed;
        if (!_flyoutDismissTimer.IsEnabled)
        {
            _flyoutDismissTimer.Start();
        }
    }

    private void StopFlyoutDismissTracking()
    {
        if (_groupWindows.Count == 0 && _folderWindows.Count == 0)
        {
            _flyoutDismissTimer.Stop();
            _leftMouseWasDown = false;
        }
    }

    private void DismissFlyoutsOnOutsideClick()
    {
        if (_groupWindows.Count == 0 && _folderWindows.Count == 0)
        {
            StopFlyoutDismissTracking();
            return;
        }

        var leftMouseIsDown = Mouse.LeftButton == MouseButtonState.Pressed;
        var justPressed = leftMouseIsDown && !_leftMouseWasDown;
        _leftMouseWasDown = leftMouseIsDown;
        if (!justPressed)
        {
            return;
        }

        var cursor = GetCursorPosition();
        if (IsPointInsideAnyWindow(cursor, _groupWindows.Values)
            || IsPointInsideAnyWindow(cursor, _folderWindows.Values)
            || IsPointInsideAnyWindow(cursor, _panelWindows.Values))
        {
            return;
        }

        CloseFlyoutWindows();
    }

    private static bool IsPointInsideAnyWindow(Point point, IEnumerable<Window> windows)
    {
        return windows.Any(window => window.IsVisible && new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight).Contains(point));
    }

    private Task OpenFolderFlyoutPathAsync(string path, string rootPath)
    {
        if (Directory.Exists(path))
        {
            if (_folderWindows.TryGetValue(rootPath, out var window)
                && window.DataContext is FolderFlyoutWindowViewModel viewModel)
            {
                LoadFolderEntries(viewModel, path);
            }

            return Task.CompletedTask;
        }

        var launcherItem = new LauncherItem(Guid.NewGuid(), Path.GetFileName(path), LauncherItemType.File, path);
        return _launchItemCommandHandler.HandleAsync(new LaunchItemCommand(launcherItem), CancellationToken.None);
    }

    private Task OpenFolderInExplorerAsync(string path)
    {
        var launcherItem = new LauncherItem(Guid.NewGuid(), Path.GetFileName(path), LauncherItemType.Folder, path);
        return _launchItemCommandHandler.HandleAsync(new LaunchItemCommand(launcherItem), CancellationToken.None);
    }

    private Task OpenGroupItemLocationAsync(GroupFlyoutItemViewModel item)
    {
        return OpenItemLocationAsync(item.Target);
    }

    private Task OpenFolderEntryLocationAsync(FolderFlyoutEntryViewModel entry)
    {
        return OpenItemLocationAsync(entry.Path);
    }

    private Task OpenItemLocationAsync(string target)
    {
        if (string.IsNullOrWhiteSpace(target)
            || target.StartsWith("group:", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("profile:", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("action:", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return Task.CompletedTask;
        }

        var isDirectory = Directory.Exists(target);
        var locationPath = isDirectory ? target : Path.GetDirectoryName(target);
        if (string.IsNullOrWhiteSpace(locationPath) || !Directory.Exists(locationPath))
        {
            return Task.CompletedTask;
        }

        try
        {
            var fullTarget = Path.GetFullPath(target);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = isDirectory
                    ? $"\"{fullTarget}\""
                    : $"/select,\"{fullTarget}\"",
                UseShellExecute = false
            })?.Dispose();
        }
        catch (Exception)
        {
            // A stale or temporarily unavailable path must not leave the UI command running.
        }

        return Task.CompletedTask;
    }

    private Task OpenItemLocationAsync(DockPanelItemViewModel item)
    {
        return OpenItemLocationAsync(item.Target);
    }

    private void LoadFolderEntries(FolderFlyoutWindowViewModel viewModel, string path)
    {
        if (!Directory.Exists(path))
        {
            viewModel.SetEntries([], path);
            return;
        }

        var directoryEntries = Directory.GetDirectories(path)
            .OrderBy(directory => Path.GetFileName(directory), StringComparer.OrdinalIgnoreCase)
            .Select(directory => new FolderFlyoutEntryViewModel(
                Path.GetFileName(directory),
                directory,
                true,
                _iconProvider.GetIcon(LauncherItemType.Folder, directory)));

        var fileEntries = Directory.GetFiles(path)
            .OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
            .Select(file =>
            {
                var draft = LauncherItemDraftFactory.Create(file);
                return new FolderFlyoutEntryViewModel(
                    Path.GetFileName(file),
                    file,
                    false,
                    _iconProvider.GetIcon(draft.Type, draft.Target));
            });

        viewModel.SetEntries(directoryEntries.Concat(fileEntries), path);
    }

    private static bool TryParseGroupTarget(string target, out Guid groupId)
    {
        const string prefix = "group:";
        if (target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(target[prefix.Length..], out groupId))
        {
            return true;
        }

        groupId = Guid.Empty;
        return false;
    }

    private static bool TryParseProfileTarget(string target, out Guid profileId)
    {
        const string prefix = "profile:";
        if (target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(target[prefix.Length..], out profileId))
        {
            return true;
        }

        profileId = Guid.Empty;
        return false;
    }

    private static bool IsDirectTargetEditable(LauncherItem item)
    {
        return item.Type != LauncherItemType.Separator
            && !item.Target.StartsWith("group:", StringComparison.OrdinalIgnoreCase)
            && !item.Target.StartsWith("profile:", StringComparison.OrdinalIgnoreCase)
            && !item.Target.StartsWith("action:", StringComparison.OrdinalIgnoreCase);
    }

    internal static Modules.Panels.Domain.Panel ClonePanelWithPosition(
        Modules.Panels.Domain.Panel panel,
        Modules.Panels.Domain.PanelPosition position,
        double? floatingLeft,
        double? floatingTop,
        double? customWidth,
        double? customHeight)
    {
        var resetDockedSize = position == Modules.Panels.Domain.PanelPosition.Floating
            && panel.Position != Modules.Panels.Domain.PanelPosition.Floating
            && customWidth is null
            && customHeight is null;
        var clone = new Modules.Panels.Domain.Panel(
            panel.Id,
            panel.Name,
            position,
            panel.LayoutMode,
            panel.Appearance with
            {
                FloatingLeft = position == Modules.Panels.Domain.PanelPosition.Floating ? floatingLeft : panel.Appearance.FloatingLeft,
                FloatingTop = position == Modules.Panels.Domain.PanelPosition.Floating ? floatingTop : panel.Appearance.FloatingTop,
                CustomWidth = resetDockedSize ? null : customWidth ?? panel.Appearance.CustomWidth,
                CustomHeight = resetDockedSize ? null : customHeight ?? panel.Appearance.CustomHeight
            });

        foreach (var itemId in panel.ItemIds)
        {
            clone.AddItem(itemId);
        }

        return clone;
    }

    private async Task DuplicateItemOnPanelAsync(Guid panelId, DockPanelItemViewModel item, CancellationToken cancellationToken = default)
    {
        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var sourceItem = workspace.Items.FirstOrDefault(candidate => candidate.Id == item.Id);
        if (sourceItem is null)
        {
            return;
        }

        var duplicatedItem = new LauncherItem(
            Guid.NewGuid(),
            $"{sourceItem.DisplayName} Copy",
            sourceItem.Type,
            sourceItem.Target,
            sourceItem.Arguments,
            sourceItem.RunAsAdministrator,
            sourceItem.IconPath);

        var updatedPanels = workspace.Panels
            .Select(candidate => candidate.Id == panelId
                ? ClonePanelWithItems(candidate, InsertAfter(candidate.ItemIds, sourceItem.Id, duplicatedItem.Id))
                : ClonePanelWithItems(candidate, candidate.ItemIds))
            .ToArray();

        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            updatedPanels,
            workspace.Items.Concat([duplicatedItem]).ToArray(),
            workspace.Groups,
            workspace.LaunchProfiles);

        await CommitRuntimeWorkspaceAsync(updatedWorkspace, $"Item '{sourceItem.DisplayName}' duplicated on panel.", cancellationToken);
    }

    private async Task RemoveItemFromPanelAsync(Guid panelId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var updatedPanels = workspace.Panels
            .Select(candidate => candidate.Id == panelId
                ? ClonePanelWithItems(candidate, candidate.ItemIds.Where(existingId => existingId != itemId))
                : ClonePanelWithItems(candidate, candidate.ItemIds))
            .ToArray();

        var stillReferenced = updatedPanels.Any(panel => panel.ItemIds.Contains(itemId))
            || workspace.Groups.Any(group => group.ItemIds.Contains(itemId))
            || workspace.LaunchProfiles.Any(profile => profile.Steps.Any(step => step.ItemId == itemId));

        var updatedItems = stillReferenced
            ? workspace.Items
            : workspace.Items.Where(item => item.Id != itemId).ToArray();

        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            updatedPanels,
            updatedItems,
            workspace.Groups,
            workspace.LaunchProfiles);

        await CommitRuntimeWorkspaceAsync(updatedWorkspace, "Item removed from panel.", cancellationToken);
    }

    private async Task DuplicateItemInGroupAsync(Guid groupId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var sourceItem = workspace.Items.FirstOrDefault(candidate => candidate.Id == itemId);
        var sourceGroup = workspace.Groups.FirstOrDefault(candidate => candidate.Id == groupId);
        if (sourceItem is null || sourceGroup is null)
        {
            return;
        }

        var duplicatedItem = new LauncherItem(
            Guid.NewGuid(),
            $"{sourceItem.DisplayName} Copy",
            sourceItem.Type,
            sourceItem.Target,
            sourceItem.Arguments,
            sourceItem.RunAsAdministrator,
            sourceItem.IconPath);
        var updatedGroups = workspace.Groups
            .Select(group => group.Id == groupId
                ? CloneGroupWithItems(group, InsertAfter(group.ItemIds, itemId, duplicatedItem.Id))
                : CloneGroupWithItems(group, group.ItemIds))
            .ToArray();
        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            workspace.Panels,
            workspace.Items.Concat([duplicatedItem]).ToArray(),
            updatedGroups,
            workspace.LaunchProfiles);

        await CommitRuntimeWorkspaceAsync(updatedWorkspace, $"Item '{sourceItem.DisplayName}' duplicated in group.", cancellationToken);
    }

    private async Task RemoveItemFromGroupAsync(Guid groupId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var sourceGroup = workspace.Groups.FirstOrDefault(group => group.Id == groupId);
        if (sourceGroup is null || !sourceGroup.ItemIds.Contains(itemId))
        {
            return;
        }

        var updatedGroups = workspace.Groups
            .Select(group => group.Id == groupId
                ? CloneGroupWithItems(group, group.ItemIds.Where(existingId => existingId != itemId))
                : CloneGroupWithItems(group, group.ItemIds))
            .ToArray();
        var stillReferenced = workspace.Panels.Any(panel => panel.ItemIds.Contains(itemId))
            || updatedGroups.Any(group => group.ItemIds.Contains(itemId))
            || workspace.LaunchProfiles.Any(profile => profile.Steps.Any(step => step.ItemId == itemId));
        var updatedItems = stillReferenced
            ? workspace.Items
            : workspace.Items.Where(item => item.Id != itemId).ToArray();
        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            workspace.Panels,
            updatedItems,
            updatedGroups,
            workspace.LaunchProfiles);

        await CommitRuntimeWorkspaceAsync(updatedWorkspace, "Item removed from group.", cancellationToken);
    }

    private async Task RenameItemAsync(Guid itemId, string currentDisplayName, CancellationToken cancellationToken = default)
    {
        var nextName = await _textPromptService.PromptAsync("Rename Item", "Enter a new label for this dock item.", currentDisplayName, cancellationToken);
        if (string.IsNullOrWhiteSpace(nextName))
        {
            return;
        }

        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var updatedItems = workspace.Items
            .Select(item => item.Id == itemId
                ? new LauncherItem(item.Id, nextName.Trim(), item.Type, item.Target, item.Arguments, item.RunAsAdministrator, item.IconPath)
                : item)
            .ToArray();

        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            workspace.Panels,
            updatedItems,
            workspace.Groups,
            workspace.LaunchProfiles);

        await CommitRuntimeWorkspaceAsync(updatedWorkspace, $"Item renamed to '{nextName.Trim()}'.", cancellationToken);
    }

    private async Task EditItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
            var item = workspace.Items.FirstOrDefault(candidate => candidate.Id == itemId);
            if (item is null)
            {
                return;
            }

            var targetEditable = IsDirectTargetEditable(item);
            var result = await _itemEditorService.EditAsync(
                new ItemEditorRequest(
                    item.DisplayName,
                    item.Type,
                    item.Target,
                    item.Arguments,
                    item.RunAsAdministrator,
                    item.IconPath,
                    targetEditable,
                    targetEditable
                        ? "Edit the visible name, icon and launch arguments here."
                        : "This item points to a group, launch profile or built-in action. You can rename it, choose an icon and adjust launch flags here."),
                cancellationToken);

            if (result is null)
            {
                return;
            }

            var updatedItems = workspace.Items
                .Select(candidate =>
                {
                    if (candidate.Id != itemId)
                    {
                        return candidate;
                    }

                    if (!targetEditable)
                    {
                        return new LauncherItem(
                            candidate.Id,
                            result.DisplayName,
                            candidate.Type,
                            candidate.Target,
                            result.Arguments,
                            result.RunAsAdministrator,
                            result.IconPath);
                    }

                    var draft = LauncherItemDraftFactory.Create(result.Target);
                    return new LauncherItem(
                        candidate.Id,
                        result.DisplayName,
                        draft.Type,
                        draft.Target,
                        string.IsNullOrWhiteSpace(result.Arguments) ? draft.Arguments : result.Arguments,
                        result.RunAsAdministrator,
                        result.IconPath);
                })
                .ToArray();

            var updatedWorkspace = new Workspace(
                workspace.SchemaVersion,
                workspace.Settings,
                workspace.Panels,
                updatedItems,
                workspace.Groups,
                workspace.LaunchProfiles);

            await CommitRuntimeWorkspaceAsync(updatedWorkspace, $"Item '{result.DisplayName}' updated.", cancellationToken);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Edit Item failed: {ex.Message}",
                "DockLauncher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task RenamePanelAsync(Guid panelId, string currentName, CancellationToken cancellationToken = default)
    {
        var nextName = await _textPromptService.PromptAsync("Rename Panel", "Enter a new panel name.", currentName, cancellationToken);
        if (string.IsNullOrWhiteSpace(nextName))
        {
            return;
        }

        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var updatedPanels = workspace.Panels
            .Select(panel => panel.Id == panelId
                ? ClonePanel(panel, nextName.Trim(), panel.Position, panel.LayoutMode, panel.Appearance, panel.ItemIds)
                : ClonePanelWithItems(panel, panel.ItemIds))
            .ToArray();

        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            updatedPanels,
            workspace.Items,
            workspace.Groups,
            workspace.LaunchProfiles);

        await CommitRuntimeWorkspaceAsync(updatedWorkspace, $"Panel renamed to '{nextName.Trim()}'.", cancellationToken);
    }

    private async Task DuplicatePanelAsync(Guid panelId, CancellationToken cancellationToken = default)
    {
        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var sourcePanel = workspace.Panels.FirstOrDefault(panel => panel.Id == panelId);
        if (sourcePanel is null)
        {
            return;
        }

        var duplicatedPanel = ClonePanel(
            Guid.NewGuid(),
            $"{sourcePanel.Name} Copy",
            sourcePanel.Position,
            sourcePanel.LayoutMode,
            sourcePanel.Appearance,
            sourcePanel.ItemIds);

        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            workspace.Panels.Concat([duplicatedPanel]).ToArray(),
            workspace.Items,
            workspace.Groups,
            workspace.LaunchProfiles);

        await CommitRuntimeWorkspaceAsync(updatedWorkspace, $"Panel '{sourcePanel.Name}' duplicated.", cancellationToken);
    }

    private async Task CreateEmptyPanelAsync(Guid referencePanelId, CancellationToken cancellationToken = default)
    {
        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var referencePanel = workspace.Panels.FirstOrDefault(panel => panel.Id == referencePanelId);
        if (referencePanel is null)
        {
            return;
        }

        var nextNumber = workspace.Panels.Count + 1;
        var emptyPanel = CreateEmptyFloatingPanel(referencePanel, nextNumber);
        var panelId = emptyPanel.Id;

        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            workspace.Panels.Concat([emptyPanel]).ToArray(),
            workspace.Items,
            workspace.Groups,
            workspace.LaunchProfiles);

        await CommitRuntimeWorkspaceAsync(updatedWorkspace, "Empty floating panel created.", cancellationToken, panelId);
    }

    internal static Modules.Panels.Domain.Panel CreateEmptyFloatingPanel(Modules.Panels.Domain.Panel referencePanel, int nextNumber)
    {
        var appearance = CreateOffsetFloatingPanelAppearance(referencePanel) with
        {
            AutoHide = false,
            Locked = false,
            Orientation = Modules.Panels.Domain.PanelOrientation.Horizontal
        };

        return ClonePanel(
            Guid.NewGuid(),
            $"Panel {nextNumber}",
            Modules.Panels.Domain.PanelPosition.Floating,
            Modules.Panels.Domain.PanelLayoutMode.IconWithLabel,
            appearance,
            []);
    }

    private static Modules.Panels.Domain.PanelAppearance CreateOffsetFloatingPanelAppearance(Modules.Panels.Domain.Panel referencePanel)
    {
        var appearance = new Modules.Panels.Domain.PanelAppearance(
            0.9,
            40,
            true,
            true,
            false,
            Locked: false,
            FloatingLeft: null,
            FloatingTop: null,
            LabelDisplayMode: Modules.Panels.Domain.PanelLabelDisplayMode.AlwaysVisible,
            LabelPlacement: Modules.Panels.Domain.PanelLabelPlacement.BelowIcon,
            IconShape: Modules.Panels.Domain.PanelIconShape.Circle,
            HorizontalPadding: 20d,
            VerticalPadding: 18d,
            LabelSpacing: 4d,
            TextSize: 10.5d,
            PanelColor: "#1B2637",
            Orientation: Modules.Panels.Domain.PanelOrientation.Horizontal);

        const double offset = 36d;
        var referenceMetrics = CalculateMetrics(referencePanel, Math.Max(referencePanel.ItemIds.Count, 1));
        var probePanel = new Modules.Panels.Domain.Panel(
            Guid.Empty,
            "Panel",
            Modules.Panels.Domain.PanelPosition.Floating,
            Modules.Panels.Domain.PanelLayoutMode.IconWithLabel,
            appearance);
        var metrics = CalculateMetrics(probePanel, 0);
        var workArea = SystemParameters.WorkArea;
        var left = ClampToWorkArea(referenceMetrics.Left + offset, metrics.Width, workArea.Left, workArea.Right);
        var top = ClampToWorkArea(referenceMetrics.Top + offset, metrics.Height, workArea.Top, workArea.Bottom);

        return appearance with
        {
            FloatingLeft = left,
            FloatingTop = top
        };
    }

    private static double ClampToWorkArea(double coordinate, double size, double min, double max)
    {
        return Math.Clamp(coordinate, min, Math.Max(min, max - size));
    }

    private async Task RemovePanelAsync(Guid panelId, CancellationToken cancellationToken = default)
    {
        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        if (workspace.Panels.Count <= 1)
        {
            return;
        }

        var updatedPanels = workspace.Panels
            .Where(panel => panel.Id != panelId)
            .Select(panel => ClonePanelWithItems(panel, panel.ItemIds))
            .ToArray();

        var stillReferencedItemIds = updatedPanels.SelectMany(panel => panel.ItemIds).ToHashSet();
        foreach (var group in workspace.Groups)
        {
            foreach (var itemId in group.ItemIds)
            {
                stillReferencedItemIds.Add(itemId);
            }
        }

        foreach (var profile in workspace.LaunchProfiles)
        {
            foreach (var step in profile.Steps)
            {
                stillReferencedItemIds.Add(step.ItemId);
            }
        }

        var updatedItems = workspace.Items
            .Where(item => stillReferencedItemIds.Contains(item.Id))
            .ToArray();

        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            updatedPanels,
            updatedItems,
            workspace.Groups,
            workspace.LaunchProfiles);

        _hiddenPanelIds.Remove(panelId);
        await CommitRuntimeWorkspaceAsync(updatedWorkspace, "Panel removed.", cancellationToken);
    }

    private async Task HidePanelAsync(Guid panelId)
    {
        _hiddenPanelIds.Add(panelId);
        if (_panelWindows.TryGetValue(panelId, out var panelWindow))
        {
            panelWindow.Hide();
        }

        await UpdatePanelAppearanceAsync(panelId, appearance => appearance with { IsHidden = true });
    }

    private Task TogglePanelLockAsync(Guid panelId, CancellationToken cancellationToken = default)
    {
        return UpdatePanelAppearanceAsync(panelId, appearance => appearance with { Locked = !appearance.Locked }, cancellationToken);
    }

    private Task TogglePanelLabelsAsync(Guid panelId, CancellationToken cancellationToken = default)
    {
        return UpdatePanelAppearanceAsync(
            panelId,
            appearance => appearance with
            {
                LabelsVisible = appearance.ResolvedLabelDisplayMode != Modules.Panels.Domain.PanelLabelDisplayMode.AlwaysVisible,
                LabelDisplayMode = appearance.ResolvedLabelDisplayMode == Modules.Panels.Domain.PanelLabelDisplayMode.AlwaysVisible
                    ? Modules.Panels.Domain.PanelLabelDisplayMode.HoverOnly
                    : Modules.Panels.Domain.PanelLabelDisplayMode.AlwaysVisible
            },
            cancellationToken);
    }

    private Task TogglePanelAlwaysOnTopAsync(Guid panelId, CancellationToken cancellationToken = default)
    {
        return UpdatePanelAppearanceAsync(panelId, appearance => appearance with { AlwaysOnTop = !appearance.AlwaysOnTop }, cancellationToken);
    }

    private Task TogglePanelAutoHideAsync(Guid panelId, CancellationToken cancellationToken = default)
    {
        return UpdatePanelAsync(
            panelId,
            panel => panel.Position == Modules.Panels.Domain.PanelPosition.Floating
                ? ClonePanel(panel, panel.Name, panel.Position, panel.LayoutMode, panel.Appearance with { AutoHide = false }, panel.ItemIds)
                : ClonePanel(panel, panel.Name, panel.Position, panel.LayoutMode, panel.Appearance with { AutoHide = !panel.Appearance.AutoHide }, panel.ItemIds),
            cancellationToken);
    }

    private Task SetPanelOrientationAsync(
        Guid panelId,
        Modules.Panels.Domain.PanelOrientation orientation,
        CancellationToken cancellationToken = default)
    {
        return UpdatePanelAsync(
            panelId,
            panel =>
            {
                var currentOrientation = ResolvePanelOrientation(panel) == Orientation.Vertical
                    ? Modules.Panels.Domain.PanelOrientation.Vertical
                    : Modules.Panels.Domain.PanelOrientation.Horizontal;

                var appearance = currentOrientation == orientation
                    ? panel.Appearance with { Orientation = orientation }
                    : panel.Appearance with
                    {
                        Orientation = orientation,
                        HorizontalPadding = panel.Appearance.VerticalPadding,
                        VerticalPadding = panel.Appearance.HorizontalPadding
                    };

                return ClonePanel(panel, panel.Name, panel.Position, panel.LayoutMode, appearance, panel.ItemIds);
            },
            cancellationToken);
    }

    private Task IncreasePanelIconSizeAsync(Guid panelId, CancellationToken cancellationToken = default)
    {
        return UpdatePanelAppearanceAsync(panelId, appearance => appearance with { IconSize = Math.Min(96, appearance.IconSize + 8) }, cancellationToken);
    }

    private Task DecreasePanelIconSizeAsync(Guid panelId, CancellationToken cancellationToken = default)
    {
        return UpdatePanelAppearanceAsync(panelId, appearance => appearance with { IconSize = Math.Max(24, appearance.IconSize - 8) }, cancellationToken);
    }

    private async Task UpdatePanelAppearanceAsync(
        Guid panelId,
        Func<Modules.Panels.Domain.PanelAppearance, Modules.Panels.Domain.PanelAppearance> updateAppearance,
        CancellationToken cancellationToken = default)
    {
        await UpdatePanelAsync(
            panelId,
            panel => ClonePanel(panel, panel.Name, panel.Position, panel.LayoutMode, updateAppearance(panel.Appearance), panel.ItemIds),
            cancellationToken);
    }

    private async Task UpdatePanelAsync(
        Guid panelId,
        Func<Modules.Panels.Domain.Panel, Modules.Panels.Domain.Panel> updatePanel,
        CancellationToken cancellationToken = default)
    {
        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var updatedPanels = workspace.Panels
            .Select(panel => panel.Id == panelId
                ? updatePanel(panel)
                : ClonePanelWithItems(panel, panel.ItemIds))
            .ToArray();

        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            updatedPanels,
            workspace.Items,
            workspace.Groups,
            workspace.LaunchProfiles);

        await CommitRuntimeWorkspaceAsync(updatedWorkspace, "Panel settings updated from runtime panel.", cancellationToken);
    }

    private async Task DuplicateItemToNewPanelAsync(Guid sourcePanelId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var sourcePanel = workspace.Panels.FirstOrDefault(panel => panel.Id == sourcePanelId);
        var sourceItem = workspace.Items.FirstOrDefault(candidate => candidate.Id == itemId);
        if (sourcePanel is null || sourceItem is null)
        {
            return;
        }

        var duplicatedItem = new LauncherItem(
            Guid.NewGuid(),
            $"{sourceItem.DisplayName} Copy",
            sourceItem.Type,
            sourceItem.Target,
            sourceItem.Arguments,
            sourceItem.RunAsAdministrator,
            sourceItem.IconPath);

        var newPanel = ClonePanel(
            Guid.NewGuid(),
            $"{sourceItem.DisplayName} Panel",
            sourcePanel.Position,
            sourcePanel.LayoutMode,
            sourcePanel.Appearance,
            [duplicatedItem.Id]);

        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            workspace.Panels.Concat([newPanel]).ToArray(),
            workspace.Items.Concat([duplicatedItem]).ToArray(),
            workspace.Groups,
            workspace.LaunchProfiles);

        await CommitRuntimeWorkspaceAsync(updatedWorkspace, $"Item '{sourceItem.DisplayName}' duplicated to a new panel.", cancellationToken);
    }

    private async Task MoveItemFromGroupToPanelAsync(
        Guid sourceGroupId,
        Guid itemId,
        Guid targetPanelId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var sourceGroup = workspace.Groups.FirstOrDefault(group => group.Id == sourceGroupId);
        var targetPanel = workspace.Panels.FirstOrDefault(panel => panel.Id == targetPanelId);
        if (sourceGroup is null
            || targetPanel is null
            || !sourceGroup.ItemIds.Contains(itemId)
            || workspace.Items.All(item => item.Id != itemId))
        {
            return;
        }

        var updatedGroups = workspace.Groups
            .Select(group => group.Id == sourceGroupId
                ? CloneGroupWithItems(group, group.ItemIds.Where(existingId => existingId != itemId))
                : CloneGroupWithItems(group, group.ItemIds))
            .ToArray();
        var updatedPanels = workspace.Panels
            .Select(panel => panel.Id == targetPanelId
                ? ClonePanelWithItems(panel, panel.ItemIds.Concat([itemId]).Distinct())
                : ClonePanelWithItems(panel, panel.ItemIds))
            .ToArray();
        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            updatedPanels,
            workspace.Items,
            updatedGroups,
            workspace.LaunchProfiles);

        await CommitRuntimeWorkspaceAsync(updatedWorkspace, "Item moved from group to panel.", cancellationToken);
    }

    private async Task MoveItemToPanelAsync(Guid sourcePanelId, Guid itemId, Guid targetPanelId, CancellationToken cancellationToken = default)
    {
        if (sourcePanelId == targetPanelId)
        {
            return;
        }

        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var updatedPanels = workspace.Panels
            .Select(candidate =>
            {
                if (candidate.Id == sourcePanelId)
                {
                    return ClonePanelWithItems(candidate, candidate.ItemIds.Where(existingId => existingId != itemId));
                }

                if (candidate.Id == targetPanelId)
                {
                    return ClonePanelWithItems(candidate, candidate.ItemIds.Concat([itemId]).Distinct());
                }

                return ClonePanelWithItems(candidate, candidate.ItemIds);
            })
            .ToArray();

        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            updatedPanels,
            workspace.Items,
            workspace.Groups,
            workspace.LaunchProfiles);

        await CommitRuntimeWorkspaceAsync(updatedWorkspace, "Item moved between panels.", cancellationToken);
    }

    private async Task MoveItemWithinPanelAsync(Guid panelId, Guid itemId, Guid? targetItemId, CancellationToken cancellationToken = default)
    {
        if (itemId == targetItemId)
        {
            return;
        }

        var workspace = await LoadRuntimeWorkspaceAsync(cancellationToken);
        var panel = workspace.Panels.SingleOrDefault(candidate => candidate.Id == panelId);
        if (panel is null || !panel.ItemIds.Contains(itemId))
        {
            return;
        }

        var itemIds = panel.ItemIds.ToList();
        itemIds.Remove(itemId);

        var targetIndex = targetItemId is null ? itemIds.Count : itemIds.IndexOf(targetItemId.Value);
        if (targetIndex < 0)
        {
            targetIndex = itemIds.Count;
        }

        itemIds.Insert(targetIndex, itemId);

        var updatedPanels = workspace.Panels
            .Select(candidate => candidate.Id == panelId
                ? ClonePanelWithItems(candidate, itemIds)
                : ClonePanelWithItems(candidate, candidate.ItemIds))
            .ToArray();

        var updatedWorkspace = new Workspace(
            workspace.SchemaVersion,
            workspace.Settings,
            updatedPanels,
            workspace.Items,
            workspace.Groups,
            workspace.LaunchProfiles);

        await CommitRuntimeWorkspaceAsync(updatedWorkspace, "Panel item order updated.", cancellationToken);
    }

    private static Modules.Panels.Domain.Panel ClonePanelWithItems(Modules.Panels.Domain.Panel panel, IEnumerable<Guid> itemIds)
    {
        return ClonePanel(panel, panel.Name, panel.Position, panel.LayoutMode, panel.Appearance, itemIds);
    }

    private static Group CloneGroupWithItems(Group group, IEnumerable<Guid> itemIds)
    {
        var clone = new Group(group.Id, group.Name);
        foreach (var itemId in itemIds)
        {
            clone.AddItem(itemId);
        }

        return clone;
    }

    private static Modules.Panels.Domain.Panel ClonePanel(
        Modules.Panels.Domain.Panel panel,
        string name,
        Modules.Panels.Domain.PanelPosition position,
        Modules.Panels.Domain.PanelLayoutMode layoutMode,
        Modules.Panels.Domain.PanelAppearance appearance,
        IEnumerable<Guid> itemIds)
    {
        return ClonePanel(panel.Id, name, position, layoutMode, appearance, itemIds);
    }

    private static Modules.Panels.Domain.Panel ClonePanel(
        Guid panelId,
        string name,
        Modules.Panels.Domain.PanelPosition position,
        Modules.Panels.Domain.PanelLayoutMode layoutMode,
        Modules.Panels.Domain.PanelAppearance appearance,
        IEnumerable<Guid> itemIds)
    {
        var clone = new Modules.Panels.Domain.Panel(
            panelId,
            name,
            position,
            layoutMode,
            appearance);

        foreach (var itemId in itemIds)
        {
            clone.AddItem(itemId);
        }

        return clone;
    }

    private static IEnumerable<Guid> InsertAfter(IEnumerable<Guid> source, Guid existingId, Guid insertedId)
    {
        var inserted = false;
        foreach (var itemId in source)
        {
            yield return itemId;
            if (!inserted && itemId == existingId)
            {
                yield return insertedId;
                inserted = true;
            }
        }

        if (!inserted)
        {
            yield return insertedId;
        }
    }

    private static Point ResolveFlyoutPosition(PanelMetrics metrics, double flyoutWidth, double flyoutHeight)
    {
        var panelBounds = new Rect(metrics.Left, metrics.Top, metrics.Width, metrics.Height);
        return DockFlyoutPlacement.Calculate(metrics.Position, panelBounds, new Size(flyoutWidth, flyoutHeight), SystemParameters.WorkArea);
    }

    private static Point GetCursorPosition()
    {
        GetCursorPos(out var point);
        return new Point(point.X, point.Y);
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;

        public static MonitorInfo Create()
        {
            return new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        }
    }
}
