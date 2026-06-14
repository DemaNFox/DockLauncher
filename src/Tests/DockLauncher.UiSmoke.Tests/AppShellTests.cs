using DockLauncher.AppHost;
using DockLauncher.AppHost.Docking;
using DockLauncher.AppHost.Hotkeys;
using DockLauncher.BuildingBlocks.Application.Contracts;
using DockLauncher.BuildingBlocks.Domain.Results;
using DockLauncher.BuildingBlocks.Presentation.Wpf;
using DockLauncher.Modules.Items.Application;
using DockLauncher.Modules.Items.Domain;
using DockLauncher.Modules.Panels.Domain;
using DockLauncher.Modules.Settings.Application;
using DockLauncher.Modules.Settings.Domain;
using DockLauncher.Modules.Settings.Presentation.Wpf;
using FluentAssertions;

namespace DockLauncher.UiSmoke.Tests;

public class AppShellTests
{
    [Fact]
    public void MainWindowViewModel_ShouldExposeChildViewModels()
    {
        var store = new StubWorkspaceStore();
        var workspace = CreateWorkspaceEditor(store);
        var viewModel = new MainWindowViewModel(workspace);

        viewModel.Workspace.Should().BeSameAs(workspace);
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldAddPanelAndItem()
    {
        var store = new StubWorkspaceStore();
        var workspace = CreateWorkspaceEditor(store);

        await workspace.LoadAsync();
        workspace.DraftPanelName = "QA";
        workspace.AddPanelCommand.Execute(null);
        workspace.DraftItemName = "Notes";
        workspace.DraftItemTarget = "notepad.exe";
        workspace.AddItemToSelectedPanelCommand.Execute(null);

        workspace.Panels.Should().Contain(x => x.Name == "QA");
        workspace.PanelItems.Should().ContainSingle(x => x.DisplayName == "Notes");
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldAddBuiltInActionToPanel()
    {
        var store = new StubWorkspaceStore();
        var workspace = CreateWorkspaceEditor(store);

        await workspace.LoadAsync();
        workspace.DraftPanelName = "System";
        workspace.AddPanelCommand.Execute(null);
        workspace.DraftItemType = LauncherItemType.Action;
        workspace.SelectedBuiltInAction = workspace.BuiltInActions.Single(action => action.Target == "action:lock");
        workspace.AddItemToSelectedPanelCommand.Execute(null);

        workspace.PanelItems.Should().ContainSingle(item => item.Type == LauncherItemType.Action && item.Target == "action:lock");
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldMoveItemAndPersistPanelAppearance()
    {
        var sourcePanel = new Panel(Guid.NewGuid(), "Work", PanelPosition.Bottom, PanelLayoutMode.IconWithLabel, new PanelAppearance(0.9, 40, true, true, false));
        var targetPanel = new Panel(Guid.NewGuid(), "Media", PanelPosition.Left, PanelLayoutMode.IconOnly, new PanelAppearance(0.75, 52, false, false, true));
        var item = new LauncherItem(Guid.NewGuid(), "Editor", LauncherItemType.Application, "notepad.exe");
        sourcePanel.AddItem(item.Id);

        var seededWorkspace = new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { sourcePanel, targetPanel }, new[] { item }, [], []);
        var store = new StubWorkspaceStore(seededWorkspace);
        var dockShellController = new StubDockShellController();
        var workspace = CreateWorkspaceEditor(store, dockShellController);

        await workspace.LoadAsync();
        workspace.SelectedPanel = workspace.Panels.Single(panel => panel.Name == "Work");
        workspace.SelectedMoveTargetPanel = workspace.Panels.Single(panel => panel.Name == "Media");
        workspace.SelectedPanel.Opacity = 0.62;
        workspace.SelectedPanel.IconSize = 56;
        workspace.SelectedPanel.LabelDisplayMode = PanelLabelDisplayMode.HoverOnly;
        workspace.MoveSelectedItemToPanelCommand.Execute(null);
        await workspace.SaveCommand.ExecuteAsync(null);

        store.LastSavedWorkspace.Should().NotBeNull();
        store.LastSavedWorkspace!.Panels.Single(panel => panel.Name == "Work").Appearance.Should().Be(
            new PanelAppearance(
                0.62,
                56,
                true,
                false,
                false,
                Locked: false,
                FloatingLeft: null,
                FloatingTop: null,
                LabelDisplayMode: PanelLabelDisplayMode.HoverOnly,
                LabelPlacement: PanelLabelPlacement.BelowIcon,
                IconShape: PanelIconShape.Circle,
                HorizontalPadding: 20d,
                VerticalPadding: 18d,
                LabelSpacing: 4d,
                TextSize: 10.5d,
                PanelColor: "#1B2637",
                Orientation: null,
                DockOffset: null,
                FlyoutDisplayMode: PanelFlyoutDisplayMode.Tiles,
                IsCollapsed: false,
                IsCollapsible: false,
                CollapseButtonSide: PanelCollapseButtonSide.Right,
                OverflowMode: PanelOverflowMode.Scroll,
                MaxOverflowTracks: 1));
        store.LastSavedWorkspace.Panels.Single(panel => panel.Name == "Work").ItemIds.Should().BeEmpty();
        store.LastSavedWorkspace.Panels.Single(panel => panel.Name == "Media").ItemIds.Should().ContainSingle().Which.Should().Be(item.Id);
        dockShellController.RefreshCalls.Should().Be(1);
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldPreviewChangesWithoutSaving()
    {
        var panel = new Panel(Guid.NewGuid(), "Work", PanelPosition.Bottom, PanelLayoutMode.IconWithLabel, new PanelAppearance(0.9, 40, true, true, false));
        var store = new StubWorkspaceStore(new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { panel }, [], [], []));
        var dockShellController = new StubDockShellController();
        var workspace = CreateWorkspaceEditor(store, dockShellController);

        await workspace.LoadAsync();
        workspace.SelectedPanel.Should().NotBeNull();
        workspace.SelectedPanel!.IconSize = 64;
        await Task.Delay(300);

        workspace.HasUnsavedChanges.Should().BeTrue();
        dockShellController.PreviewCalls.Should().BeGreaterThan(0);
        store.LastSavedWorkspace.Should().BeNull();
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldDiscardPreviewedChanges()
    {
        var panel = new Panel(Guid.NewGuid(), "Work", PanelPosition.Bottom, PanelLayoutMode.IconWithLabel, new PanelAppearance(0.9, 40, true, true, false));
        var store = new StubWorkspaceStore(new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { panel }, [], [], []));
        var dockShellController = new StubDockShellController();
        var workspace = CreateWorkspaceEditor(store, dockShellController);

        await workspace.LoadAsync();
        workspace.SelectedPanel!.Name = "Changed";
        await Task.Delay(300);
        await workspace.DiscardChangesAsync();

        workspace.HasUnsavedChanges.Should().BeFalse();
        workspace.Panels.Single().Name.Should().Be("Work");
        dockShellController.RefreshCalls.Should().Be(1);
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldPersistPanelState()
    {
        var panel = new Panel(
            Guid.NewGuid(),
            "Floating",
            PanelPosition.Floating,
            PanelLayoutMode.IconWithLabel,
            new PanelAppearance(0.9, 40, true, true, false, true, 128.5, 256.25));
        var store = new StubWorkspaceStore(new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { panel }, [], [], []));
        var workspace = CreateWorkspaceEditor(store);

        await workspace.LoadAsync();
        workspace.SelectedPanel.Should().NotBeNull();
        workspace.SelectedPanel!.Position.Should().Be(PanelPosition.Floating);
        workspace.SelectedPanel.Locked.Should().BeTrue();
        workspace.SelectedPanel.FloatingLeft.Should().Be(128.5);
        workspace.SelectedPanel.FloatingTop.Should().Be(256.25);

        workspace.SelectedPanel.Locked = false;
        workspace.SelectedPanel.FloatingLeft = 320;
        workspace.SelectedPanel.FloatingTop = 180;
        await workspace.SaveCommand.ExecuteAsync(null);

        store.LastSavedWorkspace.Should().NotBeNull();
        var savedPanel = store.LastSavedWorkspace!.Panels.Single();
        savedPanel.Position.Should().Be(PanelPosition.Floating);
        savedPanel.Appearance.Locked.Should().BeFalse();
        savedPanel.Appearance.FloatingLeft.Should().Be(320);
        savedPanel.Appearance.FloatingTop.Should().Be(180);
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldAllowRemovingLoadedPanels()
    {
        var firstPanel = new Panel(Guid.NewGuid(), "First", PanelPosition.Bottom, PanelLayoutMode.IconWithLabel, new PanelAppearance(0.9, 40, true, true, false));
        var secondPanel = new Panel(Guid.NewGuid(), "Second", PanelPosition.Top, PanelLayoutMode.IconWithLabel, new PanelAppearance(0.9, 40, true, true, true));
        var store = new StubWorkspaceStore(new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { firstPanel, secondPanel }, [], [], []));
        var workspace = CreateWorkspaceEditor(store);

        await workspace.LoadAsync();

        workspace.SelectedPanel.Should().NotBeNull();
        workspace.RemoveSelectedPanelCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void DockShellCoordinator_ShouldCreateNewEmptyPanelWithoutInheritingAutoHide()
    {
        var sourcePanel = new Panel(
            Guid.NewGuid(),
            "Hidden Top",
            PanelPosition.Top,
            PanelLayoutMode.IconWithLabel,
            new PanelAppearance(
                0.7,
                72,
                false,
                false,
                true,
                Locked: true,
                FloatingLeft: null,
                FloatingTop: null,
                LabelDisplayMode: PanelLabelDisplayMode.HoverOnly,
                LabelPlacement: PanelLabelPlacement.AboveIcon,
                IconShape: PanelIconShape.Square,
                HorizontalPadding: 4d,
                VerticalPadding: 4d,
                LabelSpacing: 1d,
                TextSize: 16d,
                PanelColor: "#FF0000",
                Orientation: PanelOrientation.Vertical));
        sourcePanel.AddItem(Guid.NewGuid());

        var emptyPanel = DockShellCoordinator.CreateEmptyFloatingPanel(sourcePanel, 2);

        emptyPanel.Name.Should().Be("Panel 2");
        emptyPanel.Position.Should().Be(PanelPosition.Floating);
        emptyPanel.LayoutMode.Should().Be(PanelLayoutMode.IconWithLabel);
        emptyPanel.ItemIds.Should().BeEmpty();
        emptyPanel.Appearance.AutoHide.Should().BeFalse();
        emptyPanel.Appearance.Locked.Should().BeFalse();
        emptyPanel.Appearance.IconSize.Should().Be(40);
        emptyPanel.Appearance.FloatingLeft.Should().NotBeNull();
        emptyPanel.Appearance.FloatingTop.Should().NotBeNull();
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldAddDroppedPathsToSelectedPanel()
    {
        var panel = new Panel(Guid.NewGuid(), "Work", PanelPosition.Bottom, PanelLayoutMode.IconWithLabel, new PanelAppearance(0.9, 40, true, true, false));
        var store = new StubWorkspaceStore(new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { panel }, [], [], []));
        var workspace = CreateWorkspaceEditor(store);

        await workspace.LoadAsync();
        workspace.AddDroppedPathsToSelectedPanel(new[]
        {
            @"C:\Temp\TestFolder",
            @"C:\Temp\App.lnk"
        });

        workspace.PanelItems.Should().HaveCount(2);
        workspace.PanelItems.Select(item => item.Target).Should().Contain(@"C:\Temp\TestFolder");
        workspace.PanelItems.Select(item => item.Target).Should().Contain(@"C:\Temp\App.lnk");
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldAddDroppedPathsToSpecificPanelById()
    {
        var selectedPanel = new Panel(Guid.NewGuid(), "Selected", PanelPosition.Bottom, PanelLayoutMode.IconWithLabel, new PanelAppearance(0.9, 40, true, true, false));
        var targetPanel = new Panel(Guid.NewGuid(), "Target", PanelPosition.Right, PanelLayoutMode.IconOnly, new PanelAppearance(0.9, 40, true, true, false));
        var dockShellController = new StubDockShellController();
        var store = new StubWorkspaceStore(new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { selectedPanel, targetPanel }, [], [], []));
        var workspace = CreateWorkspaceEditor(store, dockShellController);

        await workspace.LoadAsync();
        workspace.SelectedPanel!.Id.Should().Be(selectedPanel.Id);

        workspace.AddDroppedPathsToPanel(targetPanel.Id, new[]
        {
            @"C:\Temp\DroppedFile.txt",
            @"C:\Temp\DroppedFolder"
        }).Should().Be(2);
        await Task.Delay(300);

        workspace.SelectedPanel!.Id.Should().Be(selectedPanel.Id);
        dockShellController.LastPreviewWorkspace.Should().NotBeNull();
        var previewedTargetPanel = dockShellController.LastPreviewWorkspace!.Panels.Single(panel => panel.Id == targetPanel.Id);
        previewedTargetPanel.ItemIds.Should().HaveCount(2);
        dockShellController.LastPreviewWorkspace.Items.Select(item => item.Target).Should().Contain(@"C:\Temp\DroppedFile.txt");
        dockShellController.LastPreviewWorkspace.Items.Select(item => item.Target).Should().Contain(@"C:\Temp\DroppedFolder");
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldReorderItemsWithinSelectedPanel()
    {
        var panel = new Panel(Guid.NewGuid(), "Work", PanelPosition.Bottom, PanelLayoutMode.IconWithLabel, new PanelAppearance(0.9, 40, true, true, false));
        var itemOne = new LauncherItem(Guid.NewGuid(), "First", LauncherItemType.Application, "first.exe");
        var itemTwo = new LauncherItem(Guid.NewGuid(), "Second", LauncherItemType.Application, "second.exe");
        panel.AddItem(itemOne.Id);
        panel.AddItem(itemTwo.Id);
        var store = new StubWorkspaceStore(new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { panel }, new[] { itemOne, itemTwo }, [], []));
        var workspace = CreateWorkspaceEditor(store);

        await workspace.LoadAsync();
        workspace.MoveItemWithinSelectedPanel(itemTwo.Id, 0).Should().BeTrue();

        workspace.PanelItems.Select(item => item.Id).Should().ContainInOrder(itemTwo.Id, itemOne.Id);
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldMoveItemToAnotherPanelById()
    {
        var sourcePanel = new Panel(Guid.NewGuid(), "Source", PanelPosition.Bottom, PanelLayoutMode.IconWithLabel, new PanelAppearance(0.9, 40, true, true, false));
        var targetPanel = new Panel(Guid.NewGuid(), "Target", PanelPosition.Right, PanelLayoutMode.IconOnly, new PanelAppearance(0.9, 40, true, true, false));
        var item = new LauncherItem(Guid.NewGuid(), "File", LauncherItemType.File, @"C:\Temp\File.txt");
        sourcePanel.AddItem(item.Id);
        var store = new StubWorkspaceStore(new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { sourcePanel, targetPanel }, new[] { item }, [], []));
        var workspace = CreateWorkspaceEditor(store);

        await workspace.LoadAsync();
        workspace.MoveItemToPanel(item.Id, targetPanel.Id).Should().BeTrue();

        workspace.SelectedPanel!.Id.Should().Be(targetPanel.Id);
        workspace.PanelItems.Should().ContainSingle(current => current.Id == item.Id);
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldCreateGroupAndAddGroupLauncherToPanel()
    {
        var panel = new Panel(Guid.NewGuid(), "Work", PanelPosition.Bottom, PanelLayoutMode.IconWithLabel, new PanelAppearance(0.9, 40, true, true, false));
        var item = new LauncherItem(Guid.NewGuid(), "Editor", LauncherItemType.Application, "notepad.exe");
        panel.AddItem(item.Id);
        var store = new StubWorkspaceStore(new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { panel }, new[] { item }, [], []));
        var workspace = CreateWorkspaceEditor(store);

        await workspace.LoadAsync();
        workspace.DraftGroupName = "Development";
        workspace.AddGroupCommand.Execute(null);
        workspace.AddSelectedItemToGroupCommand.Execute(null);
        workspace.AddSelectedGroupToPanelCommand.Execute(null);
        await workspace.SaveCommand.ExecuteAsync(null);

        workspace.Groups.Should().ContainSingle(group => group.Name == "Development");
        workspace.PanelItems.Should().Contain(itemViewModel => itemViewModel.Target.StartsWith("group:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldCreateLaunchProfileAndAddProfileLauncherToPanel()
    {
        var panel = new Panel(Guid.NewGuid(), "Work", PanelPosition.Bottom, PanelLayoutMode.IconWithLabel, new PanelAppearance(0.9, 40, true, true, false));
        var item = new LauncherItem(Guid.NewGuid(), "Editor", LauncherItemType.Application, "notepad.exe");
        panel.AddItem(item.Id);
        var store = new StubWorkspaceStore(new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { panel }, new[] { item }, [], []));
        var workspace = CreateWorkspaceEditor(store);

        await workspace.LoadAsync();
        workspace.DraftLaunchProfileName = "Morning Startup";
        workspace.AddLaunchProfileCommand.Execute(null);
        workspace.AddSelectedItemToLaunchProfileCommand.Execute(null);
        workspace.AddSelectedLaunchProfileToPanelCommand.Execute(null);
        await workspace.SaveCommand.ExecuteAsync(null);

        workspace.LaunchProfiles.Should().ContainSingle(profile => profile.Name == "Morning Startup");
        workspace.SelectedLaunchProfileSteps.Should().ContainSingle(step => step.ItemId == item.Id);
        workspace.PanelItems.Should().Contain(itemViewModel => itemViewModel.Target.StartsWith("profile:", StringComparison.OrdinalIgnoreCase));
        store.LastSavedWorkspace!.LaunchProfiles.Should().ContainSingle(profile => profile.Name == "Morning Startup");
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldExportWorkspaceToChosenPath()
    {
        var store = new StubWorkspaceStore();
        var picker = new StubWorkspaceTransferPicker { ExportPath = @"C:\Temp\export-workspace.json" };
        var workspace = CreateWorkspaceEditor(store, workspaceTransferPicker: picker);

        await workspace.LoadAsync();
        await workspace.ExportWorkspaceCommand.ExecuteAsync(null);

        store.LastExportPath.Should().Be(@"C:\Temp\export-workspace.json");
        store.LastExportedWorkspace.Should().NotBeNull();
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldImportWorkspaceFromChosenPath()
    {
        var importedPanel = new Panel(Guid.NewGuid(), "Imported", PanelPosition.Right, PanelLayoutMode.IconOnly, new PanelAppearance(0.8, 44, true, true, false));
        var importedWorkspace = new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { importedPanel }, [], [], []);
        var store = new StubWorkspaceStore { ImportedWorkspace = importedWorkspace };
        var picker = new StubWorkspaceTransferPicker { ImportPath = @"C:\Temp\import-workspace.json" };
        var dockShellController = new StubDockShellController();
        var workspace = CreateWorkspaceEditor(store, dockShellController, workspaceTransferPicker: picker);

        await workspace.LoadAsync();
        await workspace.ImportWorkspaceCommand.ExecuteAsync(null);

        store.LastImportPath.Should().Be(@"C:\Temp\import-workspace.json");
        workspace.Panels.Should().ContainSingle(panel => panel.Name == "Imported");
        dockShellController.RefreshCalls.Should().Be(1);
    }

    [Fact]
    public async Task WorkspaceEditor_ShouldResetLayoutToDefaultWorkspace()
    {
        var customPanel = new Panel(Guid.NewGuid(), "Custom", PanelPosition.Left, PanelLayoutMode.IconOnly, new PanelAppearance(0.8, 44, false, false, true));
        var store = new StubWorkspaceStore(new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { customPanel }, [], [], []));
        var dockShellController = new StubDockShellController();
        var workspace = CreateWorkspaceEditor(store, dockShellController);

        await workspace.LoadAsync();
        await workspace.ResetLayoutCommand.ExecuteAsync(null);

        store.ResetCalls.Should().Be(1);
        workspace.Panels.Should().ContainSingle(panel => panel.Name == "Starter");
        dockShellController.RefreshCalls.Should().Be(1);
    }

    [Fact]
    public void DockGlobalHotkey_ShouldParseAltSpace()
    {
        DockGlobalHotkey.TryParseHotkey("Alt+Space", out var modifiers, out var virtualKey).Should().BeTrue();
        modifiers.Should().Be(0x0001);
        virtualKey.Should().Be(0x20);
    }

    [Fact]
    public void DockGlobalHotkey_ShouldRejectUnknownModifier()
    {
        DockGlobalHotkey.TryParseHotkey("Hyper+Space", out _, out _).Should().BeFalse();
    }

    [Fact]
    public void BuiltInDockActions_ShouldResolveRestartAction()
    {
        var resolved = BuiltInDockActions.TryResolve("action:restart", out var action);

        resolved.Should().BeTrue();
        action.FileName.Should().Be("shutdown.exe");
        action.Arguments.Should().Be("/r /t 0");
    }

    [Fact]
    public void DockPanelItemViewModel_ShouldExposeDockVisualMetadata()
    {
        var item = new DockLauncher.AppHost.Docking.DockPanelItemViewModel(Guid.NewGuid(), "Work Docs", LauncherItemType.Folder, @"C:\Work", null, false, null);

        item.Glyph.Should().Be("DIR");
        item.TypeLabel.Should().Be("Folder");
        item.AccentBrush.Should().NotBeNull();
        item.IconVisibility.Should().Be(System.Windows.Visibility.Collapsed);
        item.GlyphVisibility.Should().Be(System.Windows.Visibility.Visible);
    }

    [Fact]
    public async Task DockPanelItemViewModel_ContextCommands_ShouldInvokeAttachedCallbacks()
    {
        var item = new DockLauncher.AppHost.Docking.DockPanelItemViewModel(Guid.NewGuid(), "Editor", LauncherItemType.Application, "notepad.exe", null, false, null);
        var launched = false;
        var launchedAsAdmin = false;
        var openedLocation = false;
        var duplicated = false;
        var duplicatedToNewPanel = false;
        var removed = false;
        var renamed = false;
        var moved = false;

        item.AttachLauncher(_ =>
        {
            launched = true;
            return Task.CompletedTask;
        });

        item.AttachContextActions(
            _ =>
            {
                launchedAsAdmin = true;
                return Task.CompletedTask;
            },
            _ =>
            {
                openedLocation = true;
                return Task.CompletedTask;
            },
            _ =>
            {
                duplicated = true;
                return Task.CompletedTask;
            },
            _ =>
            {
                duplicatedToNewPanel = true;
                return Task.CompletedTask;
            },
            _ =>
            {
                removed = true;
                return Task.CompletedTask;
            },
            _ =>
            {
                renamed = true;
                return Task.CompletedTask;
            },
            _ => Task.CompletedTask,
            (_, _) =>
            {
                moved = true;
                return Task.CompletedTask;
            });

        await item.LaunchCommand.ExecuteAsync(null);
        await item.LaunchAsAdministratorCommand.ExecuteAsync(null);
        await item.OpenLocationCommand.ExecuteAsync(null);
        await item.DuplicateCommand.ExecuteAsync(null);
        await item.DuplicateToNewPanelCommand.ExecuteAsync(null);
        await item.RemoveCommand.ExecuteAsync(null);
        await item.RenameCommand.ExecuteAsync(null);
        await item.MoveToPanelCommand.ExecuteAsync(new DockLauncher.AppHost.Docking.DockPanelMoveTargetViewModel(Guid.NewGuid(), "Secondary"));

        launched.Should().BeTrue();
        launchedAsAdmin.Should().BeTrue();
        openedLocation.Should().BeTrue();
        duplicated.Should().BeTrue();
        duplicatedToNewPanel.Should().BeTrue();
        removed.Should().BeTrue();
        renamed.Should().BeTrue();
        moved.Should().BeTrue();
    }

    [Fact]
    public async Task DockPanelWindowViewModel_LaunchAsAdministrator_ShouldOverrideItemFlag()
    {
        DockLauncher.AppHost.Docking.DockPanelItemViewModel? capturedItem = null;
        var item = new DockLauncher.AppHost.Docking.DockPanelItemViewModel(Guid.NewGuid(), "Editor", LauncherItemType.Application, "notepad.exe", null, false, null);
        var viewModel = new DockLauncher.AppHost.Docking.DockPanelWindowViewModel(
            panelId: Guid.NewGuid(),
            title: "Main",
            position: PanelPosition.Bottom,
            itemsOrientation: System.Windows.Controls.Orientation.Horizontal,
            windowWidth: 400,
            windowHeight: 80,
            expandedWindowWidth: 400,
            expandedWindowHeight: 80,
            collapsedWindowWidth: 10,
            collapsedWindowHeight: 10,
            left: 10,
            top: 10,
            horizontalScrollEnabled: true,
            verticalScrollEnabled: false,
            isTopmost: true,
            opacity: 0.9,
            itemSize: 48,
            horizontalPadding: 20,
            verticalPadding: 18,
            labelSpacing: 4,
            textSize: 10.5,
            panelColor: "#1B2637",
            labelDisplayMode: PanelLabelDisplayMode.AlwaysVisible,
            labelPlacement: PanelLabelPlacement.BelowIcon,
            iconShape: PanelIconShape.Circle,
            overflowMode: PanelOverflowMode.Scroll,
            maxOverflowTracks: 1,
            activeOverflowTracks: 1,
            primaryVisibleSlots: 1,
            overflowActive: false,
            autoHide: false,
            isLocked: false,
            items: new[] { item },
            activateItemAsync: currentItem =>
            {
                capturedItem = currentItem;
                return Task.CompletedTask;
            },
            showConfigurator: () => { },
            refreshPanelsAsync: () => Task.CompletedTask,
            addDroppedPathsAsync: _ => Task.CompletedTask,
            addFileAsync: () => Task.CompletedTask,
            addFolderAsync: () => Task.CompletedTask,
            addSeparatorAsync: () => Task.CompletedTask,
            importPinnedShortcutsAsync: () => Task.CompletedTask,
            openGroupsEditorAsync: () => Task.CompletedTask,
            openLaunchProfilesEditorAsync: () => Task.CompletedTask,
            updatePanelPositionAsync: (_, _, _, _, _) => Task.CompletedTask,
            renamePanelAsync: () => Task.CompletedTask,
            duplicatePanelAsync: () => Task.CompletedTask,
            createEmptyPanelAsync: () => Task.CompletedTask,
            removePanelAsync: () => Task.CompletedTask,
            hidePanelAsync: () => Task.CompletedTask,
            togglePanelLockAsync: () => Task.CompletedTask,
            toggleLabelsAsync: () => Task.CompletedTask,
            toggleAlwaysOnTopAsync: () => Task.CompletedTask,
            toggleAutoHideAsync: () => Task.CompletedTask,
            setPanelOrientationAsync: _ => Task.CompletedTask,
            increaseIconSizeAsync: () => Task.CompletedTask,
            decreaseIconSizeAsync: () => Task.CompletedTask,
            openLocationAsync: _ => Task.CompletedTask,
            duplicateAsync: _ => Task.CompletedTask,
            duplicateToNewPanelAsync: _ => Task.CompletedTask,
            removeAsync: _ => Task.CompletedTask,
            renameAsync: _ => Task.CompletedTask,
            editAsync: _ => Task.CompletedTask,
            moveToPanelAsync: (_, _) => Task.CompletedTask,
            moveWithinPanelAsync: (_, _) => Task.CompletedTask,
            hasOpenFlyouts: () => false,
            exit: () => { });

        await viewModel.Items.Single().LaunchAsAdministratorCommand.ExecuteAsync(null);

        capturedItem.Should().NotBeNull();
        capturedItem!.RunAsAdministrator.Should().BeTrue();
        capturedItem.DisplayName.Should().Be("Editor");
    }

    [Fact]
    public async Task DockPanelWindowViewModel_PanelCommands_ShouldInvokeAttachedCallbacks()
    {
        var item = new DockLauncher.AppHost.Docking.DockPanelItemViewModel(Guid.NewGuid(), "Editor", LauncherItemType.Application, "notepad.exe", null, false, null);
        var renamedPanel = false;
        var duplicatedPanel = false;
        var createdPanel = false;
        var removedPanel = false;
        var hiddenPanel = false;
        var toggledLock = false;
        var toggledLabels = false;
        var toggledAlwaysOnTop = false;
        var toggledAutoHide = false;
        var increasedIconSize = false;
        var decreasedIconSize = false;

        var viewModel = new DockLauncher.AppHost.Docking.DockPanelWindowViewModel(
            panelId: Guid.NewGuid(),
            title: "Main",
            position: PanelPosition.Bottom,
            itemsOrientation: System.Windows.Controls.Orientation.Horizontal,
            windowWidth: 400,
            windowHeight: 80,
            expandedWindowWidth: 400,
            expandedWindowHeight: 80,
            collapsedWindowWidth: 10,
            collapsedWindowHeight: 10,
            left: 10,
            top: 10,
            horizontalScrollEnabled: true,
            verticalScrollEnabled: false,
            isTopmost: true,
            opacity: 0.9,
            itemSize: 48,
            horizontalPadding: 20,
            verticalPadding: 18,
            labelSpacing: 4,
            textSize: 10.5,
            panelColor: "#1B2637",
            labelDisplayMode: PanelLabelDisplayMode.AlwaysVisible,
            labelPlacement: PanelLabelPlacement.BelowIcon,
            iconShape: PanelIconShape.Circle,
            overflowMode: PanelOverflowMode.Scroll,
            maxOverflowTracks: 1,
            activeOverflowTracks: 1,
            primaryVisibleSlots: 1,
            overflowActive: false,
            autoHide: false,
            isLocked: false,
            items: new[] { item },
            activateItemAsync: _ => Task.CompletedTask,
            showConfigurator: () => { },
            refreshPanelsAsync: () => Task.CompletedTask,
            addDroppedPathsAsync: _ => Task.CompletedTask,
            addFileAsync: () => Task.CompletedTask,
            addFolderAsync: () => Task.CompletedTask,
            addSeparatorAsync: () => Task.CompletedTask,
            importPinnedShortcutsAsync: () => Task.CompletedTask,
            openGroupsEditorAsync: () => Task.CompletedTask,
            openLaunchProfilesEditorAsync: () => Task.CompletedTask,
            updatePanelPositionAsync: (_, _, _, _, _) => Task.CompletedTask,
            renamePanelAsync: () =>
            {
                renamedPanel = true;
                return Task.CompletedTask;
            },
            duplicatePanelAsync: () =>
            {
                duplicatedPanel = true;
                return Task.CompletedTask;
            },
            createEmptyPanelAsync: () =>
            {
                createdPanel = true;
                return Task.CompletedTask;
            },
            removePanelAsync: () =>
            {
                removedPanel = true;
                return Task.CompletedTask;
            },
            hidePanelAsync: () =>
            {
                hiddenPanel = true;
                return Task.CompletedTask;
            },
            togglePanelLockAsync: () =>
            {
                toggledLock = true;
                return Task.CompletedTask;
            },
            toggleLabelsAsync: () =>
            {
                toggledLabels = true;
                return Task.CompletedTask;
            },
            toggleAlwaysOnTopAsync: () =>
            {
                toggledAlwaysOnTop = true;
                return Task.CompletedTask;
            },
            toggleAutoHideAsync: () =>
            {
                toggledAutoHide = true;
                return Task.CompletedTask;
            },
            setPanelOrientationAsync: _ => Task.CompletedTask,
            increaseIconSizeAsync: () =>
            {
                increasedIconSize = true;
                return Task.CompletedTask;
            },
            decreaseIconSizeAsync: () =>
            {
                decreasedIconSize = true;
                return Task.CompletedTask;
            },
            openLocationAsync: _ => Task.CompletedTask,
            duplicateAsync: _ => Task.CompletedTask,
            duplicateToNewPanelAsync: _ => Task.CompletedTask,
            removeAsync: _ => Task.CompletedTask,
            renameAsync: _ => Task.CompletedTask,
            editAsync: _ => Task.CompletedTask,
            moveToPanelAsync: (_, _) => Task.CompletedTask,
            moveWithinPanelAsync: (_, _) => Task.CompletedTask,
            hasOpenFlyouts: () => false,
            exit: () => { });

        await viewModel.RenamePanelCommand.ExecuteAsync(null);
        await viewModel.DuplicatePanelCommand.ExecuteAsync(null);
        await viewModel.CreateEmptyPanelCommand.ExecuteAsync(null);
        await viewModel.RemovePanelCommand.ExecuteAsync(null);
        await viewModel.HidePanelCommand.ExecuteAsync(null);
        await viewModel.TogglePanelLockCommand.ExecuteAsync(null);
        await viewModel.ToggleLabelsCommand.ExecuteAsync(null);
        await viewModel.ToggleAlwaysOnTopCommand.ExecuteAsync(null);
        await viewModel.ToggleAutoHideCommand.ExecuteAsync(null);
        await viewModel.IncreaseIconSizeCommand.ExecuteAsync(null);
        await viewModel.DecreaseIconSizeCommand.ExecuteAsync(null);

        renamedPanel.Should().BeTrue();
        duplicatedPanel.Should().BeTrue();
        createdPanel.Should().BeTrue();
        removedPanel.Should().BeTrue();
        hiddenPanel.Should().BeTrue();
        toggledLock.Should().BeTrue();
        toggledLabels.Should().BeTrue();
        toggledAlwaysOnTop.Should().BeTrue();
        toggledAutoHide.Should().BeTrue();
        increasedIconSize.Should().BeTrue();
        decreasedIconSize.Should().BeTrue();
    }

    [Fact]
    public void DockPanelWindowViewModel_Overflow_ShouldComputeWholeItemScrollSteps()
    {
        var items = Enumerable.Range(0, 10)
            .Select(index => new DockLauncher.AppHost.Docking.DockPanelItemViewModel(Guid.NewGuid(), $"Item {index}", LauncherItemType.Application, "notepad.exe", null, false, null))
            .ToArray();

        var viewModel = CreateDockPanelWindowViewModelForOverflow(items, activeTracks: 2, visibleSlots: 4, overflowActive: true);

        viewModel.TotalPrimarySlots.Should().Be(5);
        viewModel.MaxOverflowScrollIndex.Should().Be(1);
        viewModel.OverflowViewportHeight.Should().Be(viewModel.PrimaryVisibleSlots * viewModel.ItemExtent);
        viewModel.OverflowViewportWidth.Should().Be(viewModel.ActiveOverflowTracks * viewModel.CrossTrackExtent);
        viewModel.ItemExtent.Should().Be(viewModel.ItemSlotHeight + viewModel.VisibleLabelHeight + viewModel.VerticalItemGap);
        viewModel.VisibleLabelHeight.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GroupFlyoutItemViewModel_ShouldExposeFallbackVisualsWithoutIcon()
    {
        var item = new DockLauncher.AppHost.Docking.GroupFlyoutItemViewModel(Guid.NewGuid(), "Editor", "notepad.exe", null, "Application");

        item.Glyph.Should().Be("APP");
        item.IconVisibility.Should().Be(System.Windows.Visibility.Collapsed);
        item.GlyphVisibility.Should().Be(System.Windows.Visibility.Visible);
    }

    [Fact]
    public async Task GroupFlyoutItemViewModel_ContextCommands_ShouldInvokeAttachedCallbacks()
    {
        var item = new DockLauncher.AppHost.Docking.GroupFlyoutItemViewModel(Guid.NewGuid(), "Editor", "notepad.exe", null, "Application");
        var launched = false;
        var launchedAsAdmin = false;
        var openedLocation = false;

        item.AttachLauncher(_ =>
        {
            launched = true;
            return Task.CompletedTask;
        });

        item.AttachContextActions(
            _ =>
            {
                launchedAsAdmin = true;
                return Task.CompletedTask;
            },
            _ =>
            {
                openedLocation = true;
                return Task.CompletedTask;
            });

        await item.LaunchCommand.ExecuteAsync(null);
        await item.LaunchAsAdministratorCommand.ExecuteAsync(null);
        await item.OpenLocationCommand.ExecuteAsync(null);

        launched.Should().BeTrue();
        launchedAsAdmin.Should().BeTrue();
        openedLocation.Should().BeTrue();
    }

    [Fact]
    public void FolderFlyoutEntryViewModel_ShouldExposeFolderGlyphWithoutIcon()
    {
        var entry = new DockLauncher.AppHost.Docking.FolderFlyoutEntryViewModel("Docs", @"C:\Docs", true, null);

        entry.Glyph.Should().Be("DIR");
        entry.IconVisibility.Should().Be(System.Windows.Visibility.Collapsed);
        entry.GlyphVisibility.Should().Be(System.Windows.Visibility.Visible);
    }

    [Fact]
    public async Task FolderFlyoutEntryViewModel_ContextCommands_ShouldInvokeAttachedCallbacks()
    {
        var entry = new DockLauncher.AppHost.Docking.FolderFlyoutEntryViewModel("Docs", @"C:\Docs", true, null);
        var opened = false;
        var openedLocation = false;

        entry.AttachOpen(_ =>
        {
            opened = true;
            return Task.CompletedTask;
        });

        entry.AttachContextActions(_ =>
        {
            openedLocation = true;
            return Task.CompletedTask;
        });

        await entry.OpenCommand.ExecuteAsync(null);
        await entry.OpenLocationCommand.ExecuteAsync(null);

        opened.Should().BeTrue();
        openedLocation.Should().BeTrue();
    }

    [Fact]
    public void DockAutoHideBehavior_ShouldCollapseLeftPanelOffscreen()
    {
        var workArea = new System.Windows.Rect(0, 0, 1920, 1080);
        var bounds = new System.Windows.Rect(12, 300, 88, 420);

        var collapsedLeft = DockLauncher.AppHost.Docking.DockAutoHideBehavior.GetCollapsedLeft(PanelPosition.Left, workArea, bounds, 10);

        collapsedLeft.Should().Be(-78);
    }

    [Fact]
    public void DockAutoHideBehavior_ShouldRevealBottomPanelNearScreenEdge()
    {
        var workArea = new System.Windows.Rect(0, 0, 1920, 1080);
        var bounds = new System.Windows.Rect(700, 990, 520, 80);

        var shouldReveal = DockLauncher.AppHost.Docking.DockAutoHideBehavior.ShouldReveal(
            PanelPosition.Bottom,
            workArea,
            bounds,
            new System.Windows.Point(960, 1072),
            42);

        shouldReveal.Should().BeTrue();
    }

    [Fact]
    public void DockAutoHideBehavior_ShouldNotRevealRightPanelOutsidePanelSpan()
    {
        var workArea = new System.Windows.Rect(0, 0, 1920, 1080);
        var bounds = new System.Windows.Rect(1910, 80, 88, 360);

        var shouldReveal = DockLauncher.AppHost.Docking.DockAutoHideBehavior.ShouldReveal(
            PanelPosition.Right,
            workArea,
            bounds,
            new System.Windows.Point(1918, 900),
            42);

        shouldReveal.Should().BeFalse();
    }

    [Fact]
    public void DockMagnificationBehavior_ShouldPeakAtCursorCenter()
    {
        var scale = DockLauncher.AppHost.Docking.DockMagnificationBehavior.ComputeScale(0);

        scale.Should().BeApproximately(1.18, 0.001);
    }

    [Fact]
    public void DockMagnificationBehavior_ShouldReturnBaseScaleOutsideRange()
    {
        var scale = DockLauncher.AppHost.Docking.DockMagnificationBehavior.ComputeScale(400);

        scale.Should().Be(1.0);
    }

    [Fact]
    public void DockMagnificationBehavior_ShouldUseHorizontalAxisForBottomDock()
    {
        var distance = DockLauncher.AppHost.Docking.DockMagnificationBehavior.ComputeAxisDistance(
            System.Windows.Controls.Orientation.Horizontal,
            220,
            500,
            180,
            300);

        distance.Should().Be(40);
    }

    [Fact]
    public void DockMagnificationBehavior_ShouldUseVerticalAxisForSideDock()
    {
        var distance = DockLauncher.AppHost.Docking.DockMagnificationBehavior.ComputeAxisDistance(
            System.Windows.Controls.Orientation.Vertical,
            220,
            500,
            180,
            420);

        distance.Should().Be(80);
    }

    [Fact]
    public void DockFlyoutPlacement_ShouldOpenBottomPanelFlyoutAbovePanel()
    {
        var placement = DockLauncher.AppHost.Docking.DockFlyoutPlacement.Calculate(
            PanelPosition.Bottom,
            new System.Windows.Rect(700, 980, 500, 88),
            new System.Windows.Size(320, 360),
            new System.Windows.Rect(0, 0, 1920, 1080));

        placement.Y.Should().BeLessThan(980);
    }

    [Fact]
    public void DockFlyoutPlacement_ShouldOpenLeftPanelFlyoutToTheRight()
    {
        var placement = DockLauncher.AppHost.Docking.DockFlyoutPlacement.Calculate(
            PanelPosition.Left,
            new System.Windows.Rect(12, 280, 88, 420),
            new System.Windows.Size(320, 360),
            new System.Windows.Rect(0, 0, 1920, 1080));

        placement.X.Should().BeGreaterThan(100);
    }

    private static DockLauncher.AppHost.Docking.DockPanelWindowViewModel CreateDockPanelWindowViewModelForOverflow(
        IReadOnlyList<DockLauncher.AppHost.Docking.DockPanelItemViewModel> items,
        int activeTracks,
        int visibleSlots,
        bool overflowActive)
    {
        return new DockLauncher.AppHost.Docking.DockPanelWindowViewModel(
            panelId: Guid.NewGuid(),
            title: "Overflow",
            position: PanelPosition.Left,
            itemsOrientation: System.Windows.Controls.Orientation.Vertical,
            windowWidth: 160,
            windowHeight: 420,
            expandedWindowWidth: 160,
            expandedWindowHeight: 420,
            collapsedWindowWidth: 10,
            collapsedWindowHeight: 10,
            left: 10,
            top: 10,
            horizontalScrollEnabled: false,
            verticalScrollEnabled: overflowActive,
            isTopmost: true,
            opacity: 0.9,
            itemSize: 48,
            horizontalPadding: 20,
            verticalPadding: 18,
            labelSpacing: 4,
            textSize: 10.5,
            panelColor: "#1B2637",
            labelDisplayMode: PanelLabelDisplayMode.AlwaysVisible,
            labelPlacement: PanelLabelPlacement.BelowIcon,
            iconShape: PanelIconShape.Circle,
            overflowMode: PanelOverflowMode.ExpandLayout,
            maxOverflowTracks: activeTracks,
            activeOverflowTracks: activeTracks,
            primaryVisibleSlots: visibleSlots,
            overflowActive: overflowActive,
            autoHide: false,
            isLocked: false,
            items: items,
            activateItemAsync: _ => Task.CompletedTask,
            showConfigurator: () => { },
            refreshPanelsAsync: () => Task.CompletedTask,
            addDroppedPathsAsync: _ => Task.CompletedTask,
            addFileAsync: () => Task.CompletedTask,
            addFolderAsync: () => Task.CompletedTask,
            addSeparatorAsync: () => Task.CompletedTask,
            importPinnedShortcutsAsync: () => Task.CompletedTask,
            openGroupsEditorAsync: () => Task.CompletedTask,
            openLaunchProfilesEditorAsync: () => Task.CompletedTask,
            updatePanelPositionAsync: (_, _, _, _, _) => Task.CompletedTask,
            renamePanelAsync: () => Task.CompletedTask,
            duplicatePanelAsync: () => Task.CompletedTask,
            createEmptyPanelAsync: () => Task.CompletedTask,
            removePanelAsync: () => Task.CompletedTask,
            hidePanelAsync: () => Task.CompletedTask,
            togglePanelLockAsync: () => Task.CompletedTask,
            toggleLabelsAsync: () => Task.CompletedTask,
            toggleAlwaysOnTopAsync: () => Task.CompletedTask,
            toggleAutoHideAsync: () => Task.CompletedTask,
            setPanelOrientationAsync: _ => Task.CompletedTask,
            increaseIconSizeAsync: () => Task.CompletedTask,
            decreaseIconSizeAsync: () => Task.CompletedTask,
            openLocationAsync: _ => Task.CompletedTask,
            duplicateAsync: _ => Task.CompletedTask,
            duplicateToNewPanelAsync: _ => Task.CompletedTask,
            removeAsync: _ => Task.CompletedTask,
            renameAsync: _ => Task.CompletedTask,
            editAsync: _ => Task.CompletedTask,
            moveToPanelAsync: (_, _) => Task.CompletedTask,
            moveWithinPanelAsync: (_, _) => Task.CompletedTask,
            hasOpenFlyouts: () => false,
            exit: () => { });
    }

    private static WorkspaceEditorViewModel CreateWorkspaceEditor(
        StubWorkspaceStore store,
        StubDockShellController? dockShellController = null,
        StubItemTargetPicker? itemTargetPicker = null,
        StubPanelColorPicker? panelColorPicker = null,
        StubWorkspaceTransferPicker? workspaceTransferPicker = null)
    {
        return new WorkspaceEditorViewModel(
            new LoadWorkspaceQueryHandler(store),
            new SaveWorkspaceCommandHandler(store),
            new LaunchItemCommandHandler(new StubLauncherItemService()),
            dockShellController ?? new StubDockShellController(),
            new StubItemIconProvider(),
            itemTargetPicker ?? new StubItemTargetPicker(),
            panelColorPicker ?? new StubPanelColorPicker(),
            store,
            workspaceTransferPicker ?? new StubWorkspaceTransferPicker());
    }

    private sealed class StubItemIconProvider : IItemIconProvider
    {
        public System.Windows.Media.ImageSource? GetIcon(LauncherItemType type, string target, string? iconPath = null)
        {
            return null;
        }
    }

    private sealed class StubWorkspaceStore : IWorkspaceStore
    {
        private Workspace _workspace;

        public StubWorkspaceStore()
            : this(new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), [], [], [], []))
        {
        }

        public StubWorkspaceStore(Workspace workspace)
        {
            _workspace = workspace;
        }

        public Workspace? LastSavedWorkspace { get; private set; }

        public Workspace? LastExportedWorkspace { get; private set; }

        public Workspace? ImportedWorkspace { get; set; }

        public string? LastExportPath { get; private set; }

        public string? LastImportPath { get; private set; }

        public int ResetCalls { get; private set; }

        public Task<Workspace> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_workspace);
        }

        public Task SaveAsync(Workspace workspace, CancellationToken cancellationToken)
        {
            LastSavedWorkspace = workspace;
            _workspace = workspace;
            return Task.CompletedTask;
        }

        public Task ExportAsync(Workspace workspace, string path, CancellationToken cancellationToken)
        {
            LastExportedWorkspace = workspace;
            LastExportPath = path;
            return Task.CompletedTask;
        }

        public Task<Workspace> ImportAsync(string path, CancellationToken cancellationToken)
        {
            LastImportPath = path;
            _workspace = ImportedWorkspace ?? _workspace;
            return Task.FromResult(_workspace);
        }

        public Task<Workspace> ResetAsync(CancellationToken cancellationToken)
        {
            ResetCalls++;
            var panel = new Panel(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Starter", PanelPosition.Bottom, PanelLayoutMode.IconWithLabel, new PanelAppearance(0.9, 40, true, true, false));
            var item = new LauncherItem(Guid.Parse("44444444-4444-4444-4444-444444444444"), "Explorer", LauncherItemType.Application, "explorer.exe");
            panel.AddItem(item.Id);
            _workspace = new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { panel }, new[] { item }, [], []);
            return Task.FromResult(_workspace);
        }
    }

    private sealed class StubLauncherItemService : ILauncherItemService
    {
        public Task<Result> LaunchAsync(LauncherItem item, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class StubDockShellController : IDockShellController
    {
        public int AddPathCalls { get; private set; }

        public int PreviewCalls { get; private set; }

        public int RefreshCalls { get; private set; }

        public int UpdatePositionCalls { get; private set; }

        public Workspace? LastPreviewWorkspace { get; private set; }

        public Task AddPathsToPanelAsync(Guid panelId, IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
        {
            AddPathCalls++;
            return Task.CompletedTask;
        }

        public void Exit()
        {
        }

        public Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            return Task.CompletedTask;
        }

        public Task PreviewWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default)
        {
            PreviewCalls++;
            LastPreviewWorkspace = workspace;
            return Task.CompletedTask;
        }

        public Task UpdatePanelPositionAsync(
            Guid panelId,
            PanelPosition position,
            double? floatingLeft = null,
            double? floatingTop = null,
            double? customWidth = null,
            double? customHeight = null,
            CancellationToken cancellationToken = default)
        {
            UpdatePositionCalls++;
            return Task.CompletedTask;
        }

        public void TogglePanelsVisibility()
        {
        }

        public void EnsurePanelsVisible()
        {
        }

        public void ShowHiddenPanels()
        {
        }

        public void ShowConfigurator()
        {
        }
    }

    private sealed class StubItemTargetPicker : IItemTargetPicker
    {
        public Task<string?> PickFileAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class StubWorkspaceTransferPicker : IWorkspaceTransferPicker
    {
        public string? ImportPath { get; set; }

        public string? ExportPath { get; set; }

        public Task<string?> PickImportFileAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ImportPath);
        }

        public Task<string?> PickExportFileAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExportPath);
        }
    }

    private sealed class StubPanelColorPicker : IPanelColorPicker
    {
        public string? Color { get; set; }

        public Task<string?> PickColorAsync(string? currentColor, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Color);
        }
    }
}
