using DockLauncher.BuildingBlocks.Infrastructure.FileSystem;
using DockLauncher.BuildingBlocks.Infrastructure.Serialization;
using DockLauncher.Modules.Groups.Domain;
using DockLauncher.Modules.Items.Domain;
using DockLauncher.Modules.LaunchProfiles.Domain;
using DockLauncher.Modules.Panels.Domain;
using DockLauncher.Modules.Settings.Domain;
using DockLauncher.Modules.Settings.Infrastructure;
using FluentAssertions;
using System.IO.Abstractions.TestingHelpers;

namespace DockLauncher.Integration.Tests;

public class JsonWorkspaceStoreTests
{
    [Fact]
    public async Task LoadAsync_ShouldReturnDefaultWorkspace_WhenFileDoesNotExist()
    {
        var root = @"C:\Users\Test\AppData\Roaming\DockLauncher";
        var fileSystem = new MockFileSystem();
        var store = new JsonWorkspaceStore(fileSystem, new SystemTextJsonSerializer(), new AppDataPaths(root));

        var workspace = await store.LoadAsync(CancellationToken.None);

        workspace.Panels.Should().NotBeEmpty();
        workspace.Items.Should().NotBeEmpty();
        fileSystem.File.Exists(Path.Combine(root, "workspace.json")).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_ShouldPreservePanelItemAssignments()
    {
        var root = @"C:\Users\Test\AppData\Roaming\DockLauncher";
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
        var serializer = new SystemTextJsonSerializer();
        var paths = new AppDataPaths(root);
        var store = new JsonWorkspaceStore(fileSystem, serializer, paths);
        var panel = new Panel(Guid.NewGuid(), "Work", PanelPosition.Bottom, PanelLayoutMode.IconWithLabel, new PanelAppearance(0.9, 40, true, true, false));
        var item = new LauncherItem(Guid.NewGuid(), "Editor", LauncherItemType.Application, "notepad.exe");
        panel.AddItem(item.Id);
        var workspace = new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { panel }, new[] { item }, [], []);

        await store.SaveAsync(workspace, CancellationToken.None);
        var reloaded = await store.LoadAsync(CancellationToken.None);

        reloaded.Panels.Should().ContainSingle();
        reloaded.Panels[0].ItemIds.Should().ContainSingle().Which.Should().Be(item.Id);
        reloaded.Items.Should().ContainSingle(x => x.Id == item.Id);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_ShouldPreservePanelAppearance()
    {
        var root = @"C:\Users\Test\AppData\Roaming\DockLauncher";
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
        var serializer = new SystemTextJsonSerializer();
        var paths = new AppDataPaths(root);
        var store = new JsonWorkspaceStore(fileSystem, serializer, paths);
        var panel = new Panel(
            Guid.NewGuid(),
            "Media",
            PanelPosition.Floating,
            PanelLayoutMode.Tiles,
            new PanelAppearance(0.55, 64, false, false, true, true, 144.5, 288.25, PanelLabelDisplayMode.HoverOnly, PanelLabelPlacement.AboveIcon, PanelIconShape.Hexagon, 28, 22, 9, 13.5, "#223344"));
        var workspace = new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { panel }, [], [], []);

        await store.SaveAsync(workspace, CancellationToken.None);
        var reloaded = await store.LoadAsync(CancellationToken.None);

        reloaded.Panels.Should().ContainSingle();
        reloaded.Panels[0].Position.Should().Be(PanelPosition.Floating);
        reloaded.Panels[0].LayoutMode.Should().Be(PanelLayoutMode.Tiles);
        reloaded.Panels[0].Appearance.Should().Be(
            new PanelAppearance(0.55, 64, false, false, true, true, 144.5, 288.25, PanelLabelDisplayMode.HoverOnly, PanelLabelPlacement.AboveIcon, PanelIconShape.Hexagon, 28, 22, 9, 13.5, "#223344"));
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_ShouldPreserveGroups()
    {
        var root = @"C:\Users\Test\AppData\Roaming\DockLauncher";
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
        var serializer = new SystemTextJsonSerializer();
        var paths = new AppDataPaths(root);
        var store = new JsonWorkspaceStore(fileSystem, serializer, paths);
        var item = new LauncherItem(Guid.NewGuid(), "Editor", LauncherItemType.Application, "notepad.exe");
        var group = new Group(Guid.NewGuid(), "Development");
        group.AddItem(item.Id);
        var workspace = new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), [], new[] { item }, new[] { group }, []);

        await store.SaveAsync(workspace, CancellationToken.None);
        var reloaded = await store.LoadAsync(CancellationToken.None);

        reloaded.Groups.Should().ContainSingle();
        reloaded.Groups[0].Name.Should().Be("Development");
        reloaded.Groups[0].ItemIds.Should().ContainSingle().Which.Should().Be(item.Id);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_ShouldPreserveLaunchProfiles()
    {
        var root = @"C:\Users\Test\AppData\Roaming\DockLauncher";
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
        var serializer = new SystemTextJsonSerializer();
        var paths = new AppDataPaths(root);
        var store = new JsonWorkspaceStore(fileSystem, serializer, paths);
        var item = new LauncherItem(Guid.NewGuid(), "Editor", LauncherItemType.Application, "notepad.exe");
        var profile = new LaunchProfile(Guid.NewGuid(), "Morning Startup", new[] { new LaunchStep(item.Id, 1500, true) });
        var workspace = new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), [], new[] { item }, [], new[] { profile });

        await store.SaveAsync(workspace, CancellationToken.None);
        var reloaded = await store.LoadAsync(CancellationToken.None);

        reloaded.LaunchProfiles.Should().ContainSingle();
        reloaded.LaunchProfiles[0].Name.Should().Be("Morning Startup");
        reloaded.LaunchProfiles[0].Steps.Should().ContainSingle();
        reloaded.LaunchProfiles[0].Steps[0].ItemId.Should().Be(item.Id);
        reloaded.LaunchProfiles[0].Steps[0].DelayMs.Should().Be(1500);
        reloaded.LaunchProfiles[0].Steps[0].RunAsAdministrator.Should().BeTrue();
    }

    [Fact]
    public async Task ExportAsync_ThenImportAsync_ShouldPreserveWorkspace()
    {
        var root = @"C:\Users\Test\AppData\Roaming\DockLauncher";
        var exportPath = @"C:\Users\Test\Desktop\workspace-export.json";
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
        var serializer = new SystemTextJsonSerializer();
        var paths = new AppDataPaths(root);
        var store = new JsonWorkspaceStore(fileSystem, serializer, paths);
        var panel = new Panel(Guid.NewGuid(), "Portable", PanelPosition.Top, PanelLayoutMode.IconOnly, new PanelAppearance(0.7, 36, true, false, false));
        var item = new LauncherItem(Guid.NewGuid(), "Editor", LauncherItemType.Application, "notepad.exe");
        panel.AddItem(item.Id);
        var workspace = new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { panel }, new[] { item }, [], []);

        await store.ExportAsync(workspace, exportPath, CancellationToken.None);
        var imported = await store.ImportAsync(exportPath, CancellationToken.None);

        imported.Panels.Should().ContainSingle(panelResult => panelResult.Name == "Portable");
        imported.Items.Should().ContainSingle(itemResult => itemResult.DisplayName == "Editor");
    }

    [Fact]
    public async Task ResetAsync_ShouldRestoreDefaultWorkspace()
    {
        var root = @"C:\Users\Test\AppData\Roaming\DockLauncher";
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
        var serializer = new SystemTextJsonSerializer();
        var paths = new AppDataPaths(root);
        var store = new JsonWorkspaceStore(fileSystem, serializer, paths);
        var customPanel = new Panel(Guid.NewGuid(), "Custom", PanelPosition.Left, PanelLayoutMode.IconOnly, new PanelAppearance(0.6, 32, false, false, true));
        var customWorkspace = new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), new[] { customPanel }, [], [], []);
        await store.SaveAsync(customWorkspace, CancellationToken.None);

        var resetWorkspace = await store.ResetAsync(CancellationToken.None);

        resetWorkspace.Panels.Should().ContainSingle(panel => panel.Name == "Starter");
        resetWorkspace.Items.Should().ContainSingle(item => item.DisplayName == "Explorer");
        fileSystem.File.Exists(Path.Combine(root, "workspace.json")).Should().BeTrue();
    }
}
