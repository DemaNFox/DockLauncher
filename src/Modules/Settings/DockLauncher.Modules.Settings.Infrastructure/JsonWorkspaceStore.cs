using System.IO.Abstractions;
using DockLauncher.BuildingBlocks.Application.Contracts;
using DockLauncher.BuildingBlocks.Infrastructure.FileSystem;
using DockLauncher.Modules.Groups.Domain;
using DockLauncher.Modules.Items.Domain;
using DockLauncher.Modules.LaunchProfiles.Domain;
using DockLauncher.Modules.Panels.Domain;
using DockLauncher.Modules.Settings.Application;
using DockLauncher.Modules.Settings.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.Modules.Settings.Infrastructure;

public static class SettingsModule
{
    public static IServiceCollection AddSettingsModule(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IWorkspaceStore, JsonWorkspaceStore>();
        services.AddTransient<LoadWorkspaceQueryHandler>();
        services.AddTransient<SaveWorkspaceCommandHandler>();
        return services;
    }
}

public sealed class JsonWorkspaceStore : IWorkspaceStore
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

    private readonly IFileSystem _fileSystem;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly AppDataPaths _paths;

    public JsonWorkspaceStore(IFileSystem fileSystem, IJsonSerializer jsonSerializer, AppDataPaths paths)
    {
        _fileSystem = fileSystem;
        _jsonSerializer = jsonSerializer;
        _paths = paths;
    }

    public async Task<Workspace> LoadAsync(CancellationToken cancellationToken)
    {
        if (!_fileSystem.File.Exists(_paths.WorkspaceFilePath))
        {
            var workspace = CreateDefaultWorkspace();
            await SaveAsync(workspace, cancellationToken);
            return workspace;
        }

        var json = await _fileSystem.File.ReadAllTextAsync(_paths.WorkspaceFilePath, cancellationToken);
        var storage = _jsonSerializer.Deserialize<WorkspaceStorageModel>(json);
        return storage is null ? CreateDefaultWorkspace() : MapToDomain(storage);
    }

    public async Task SaveAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        _fileSystem.Directory.CreateDirectory(_paths.Root);
        await WriteWorkspaceAsync(workspace, _paths.WorkspaceFilePath, cancellationToken);
    }

    public Task ExportAsync(Workspace workspace, string path, CancellationToken cancellationToken)
    {
        return WriteWorkspaceAsync(workspace, path, cancellationToken);
    }

    public async Task<Workspace> ImportAsync(string path, CancellationToken cancellationToken)
    {
        var json = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken);
        var storage = _jsonSerializer.Deserialize<WorkspaceStorageModel>(json);
        return storage is null ? CreateDefaultWorkspace() : MapToDomain(storage);
    }

    public async Task<Workspace> ResetAsync(CancellationToken cancellationToken)
    {
        var workspace = CreateDefaultWorkspace();
        await SaveAsync(workspace, cancellationToken);
        return workspace;
    }

    private static Workspace MapToDomain(WorkspaceStorageModel storage)
    {
        var panels = storage.Panels.Select(panelStorage =>
        {
            var panel = new Panel(
                panelStorage.Id,
                panelStorage.Name,
                panelStorage.Position,
                panelStorage.LayoutMode,
                new PanelAppearance(
                    panelStorage.Appearance.Opacity,
                    panelStorage.Appearance.IconSize,
                    panelStorage.Appearance.AlwaysOnTop,
                    panelStorage.Appearance.LabelsVisible,
                    panelStorage.Appearance.AutoHide,
                    panelStorage.Appearance.Locked,
                    panelStorage.Appearance.FloatingLeft,
                    panelStorage.Appearance.FloatingTop,
                    panelStorage.Appearance.LabelDisplayMode,
                    panelStorage.Appearance.LabelPlacement,
                    panelStorage.Appearance.IconShape,
                    panelStorage.Appearance.HorizontalPadding,
                    panelStorage.Appearance.VerticalPadding,
                    panelStorage.Appearance.LabelSpacing,
                    panelStorage.Appearance.TextSize,
                    panelStorage.Appearance.PanelColor,
                    panelStorage.Appearance.Orientation,
                    panelStorage.Appearance.DockOffset,
                    panelStorage.Appearance.FlyoutDisplayMode,
                    panelStorage.Appearance.GroupOpenMode,
                    panelStorage.Appearance.IsCollapsed,
                    panelStorage.Appearance.IsCollapsible,
                    panelStorage.Appearance.CollapseButtonSide,
                    panelStorage.Appearance.AutoCollapse,
                    panelStorage.Appearance.AutoCollapseDelaySeconds,
                    panelStorage.Appearance.OverflowMode,
                    panelStorage.Appearance.MaxOverflowTracks,
                    panelStorage.Appearance.IsHidden,
                    panelStorage.Appearance.CustomWidth,
                    panelStorage.Appearance.CustomHeight));

            foreach (var itemId in panelStorage.ItemIds)
            {
                panel.AddItem(itemId);
            }

            return panel;
        }).ToArray();

        var items = storage.Items.Select(itemStorage => new LauncherItem(
            itemStorage.Id,
            itemStorage.DisplayName,
            itemStorage.Type,
            itemStorage.Target,
            itemStorage.Arguments,
            itemStorage.RunAsAdministrator,
            itemStorage.IconPath)).ToArray();

        var groups = (storage.Groups ?? []).Select(groupStorage =>
        {
            var group = new Group(groupStorage.Id, groupStorage.Name);
            foreach (var itemId in groupStorage.ItemIds)
            {
                group.AddItem(itemId);
            }

            return group;
        }).ToArray();

        var launchProfiles = (storage.LaunchProfiles ?? []).Select(profileStorage =>
            new LaunchProfile(
                profileStorage.Id,
                profileStorage.Name,
                profileStorage.Steps.Select(step => new LaunchStep(step.ItemId, step.DelayMs, step.RunAsAdministrator)).ToArray()))
            .ToArray();

        return new Workspace(
            storage.SchemaVersion,
            new AppSettings(
                storage.Settings.Language,
                storage.Settings.Theme,
                storage.Settings.StartWithWindows,
                storage.Settings.GlobalHotkey),
            panels,
            items,
            groups,
            launchProfiles);
    }

    private static WorkspaceStorageModel MapToStorage(Workspace workspace)
    {
        return new WorkspaceStorageModel(
            workspace.SchemaVersion,
            new AppSettingsStorageModel(
                workspace.Settings.Language,
                workspace.Settings.Theme,
                workspace.Settings.StartWithWindows,
                workspace.Settings.GlobalHotkey),
            workspace.Panels.Select(panel => new PanelStorageModel(
                panel.Id,
                panel.Name,
                panel.Position,
                panel.LayoutMode,
                new PanelAppearanceStorageModel(
                    panel.Appearance.Opacity,
                    panel.Appearance.IconSize,
                    panel.Appearance.AlwaysOnTop,
                    panel.Appearance.ResolvedLabelDisplayMode == PanelLabelDisplayMode.AlwaysVisible,
                    panel.Appearance.AutoHide,
                    panel.Appearance.Locked,
                    panel.Appearance.FloatingLeft,
                    panel.Appearance.FloatingTop,
                    panel.Appearance.LabelDisplayMode,
                    panel.Appearance.LabelPlacement,
                    panel.Appearance.IconShape,
                    panel.Appearance.HorizontalPadding,
                    panel.Appearance.VerticalPadding,
                    panel.Appearance.LabelSpacing,
                    panel.Appearance.TextSize,
                    panel.Appearance.PanelColor,
                    panel.Appearance.Orientation,
                    panel.Appearance.DockOffset,
                    panel.Appearance.FlyoutDisplayMode,
                    panel.Appearance.GroupOpenMode,
                    panel.Appearance.IsCollapsed,
                    panel.Appearance.IsCollapsible,
                    panel.Appearance.CollapseButtonSide,
                    panel.Appearance.AutoCollapse,
                    panel.Appearance.AutoCollapseDelaySeconds,
                    panel.Appearance.OverflowMode,
                    panel.Appearance.ResolvedMaxOverflowTracks,
                    panel.Appearance.IsHidden,
                    panel.Appearance.CustomWidth,
                    panel.Appearance.CustomHeight),
                panel.ItemIds.ToArray())).ToArray(),
            workspace.Items.Select(item => new LauncherItemStorageModel(
                item.Id,
                item.DisplayName,
                item.Type,
                item.Target,
                item.Arguments,
                item.RunAsAdministrator,
                item.IconPath)).ToArray(),
            workspace.Groups.Select(group => new GroupStorageModel(
                group.Id,
                group.Name,
                group.ItemIds.ToArray())).ToArray(),
            workspace.LaunchProfiles.Select(profile => new LaunchProfileStorageModel(
                profile.Id,
                profile.Name,
                profile.Steps.Select(step => new LaunchStepStorageModel(step.ItemId, step.DelayMs, step.RunAsAdministrator)).ToArray())).ToArray());
    }

    private Workspace CreateDefaultWorkspace()
    {
        var panel = new Panel(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Starter",
            PanelPosition.Bottom,
            PanelLayoutMode.IconWithLabel,
            new PanelAppearance(0.9, 40, true, true, false));

        var items = LoadPinnedShortcutItems().ToList();
        if (items.Count == 0)
        {
            items.Add(new LauncherItem(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                "Explorer",
                LauncherItemType.Application,
                "explorer.exe"));
        }

        foreach (var item in items)
        {
            panel.AddItem(item.Id);
        }

        return new Workspace(
            1,
            new AppSettings("en", "system", false, "Alt+Space"),
            new[] { panel },
            items.ToArray(),
            [],
            []);
    }

    private IEnumerable<LauncherItem> LoadPinnedShortcutItems()
    {
        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in PinnedShortcutDirectories.Where(_fileSystem.Directory.Exists))
        {
            foreach (var shortcutPath in _fileSystem.Directory.EnumerateFiles(directory, "*.lnk", SearchOption.TopDirectoryOnly))
            {
                if (!seenTargets.Add(shortcutPath))
                {
                    continue;
                }

                var displayName = Path.GetFileNameWithoutExtension(shortcutPath);
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                yield return new LauncherItem(
                    Guid.NewGuid(),
                    displayName,
                    LauncherItemType.Shortcut,
                    shortcutPath);
            }
        }
    }

    private async Task WriteWorkspaceAsync(Workspace workspace, string path, CancellationToken cancellationToken)
    {
        var directory = _fileSystem.Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _fileSystem.Directory.CreateDirectory(directory);
        }

        var json = _jsonSerializer.Serialize(MapToStorage(workspace));
        await _fileSystem.File.WriteAllTextAsync(path, json, cancellationToken);
    }
}

internal sealed record WorkspaceStorageModel(
    int SchemaVersion,
    AppSettingsStorageModel Settings,
    IReadOnlyList<PanelStorageModel> Panels,
    IReadOnlyList<LauncherItemStorageModel> Items,
    IReadOnlyList<GroupStorageModel>? Groups,
    IReadOnlyList<LaunchProfileStorageModel>? LaunchProfiles);

internal sealed record AppSettingsStorageModel(
    string Language,
    string Theme,
    bool StartWithWindows,
    string GlobalHotkey);

internal sealed record PanelStorageModel(
    Guid Id,
    string Name,
    PanelPosition Position,
    PanelLayoutMode LayoutMode,
    PanelAppearanceStorageModel Appearance,
    IReadOnlyList<Guid> ItemIds);

internal sealed record PanelAppearanceStorageModel(
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
    double? CustomHeight = null);

internal sealed record LauncherItemStorageModel(
    Guid Id,
    string DisplayName,
    LauncherItemType Type,
    string Target,
    string? Arguments,
    bool RunAsAdministrator,
    string? IconPath = null);

internal sealed record GroupStorageModel(
    Guid Id,
    string Name,
    IReadOnlyList<Guid> ItemIds);

internal sealed record LaunchProfileStorageModel(
    Guid Id,
    string Name,
    IReadOnlyList<LaunchStepStorageModel> Steps);

internal sealed record LaunchStepStorageModel(
    Guid ItemId,
    int DelayMs,
    bool RunAsAdministrator);
