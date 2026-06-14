using DockLauncher.Modules.Groups.Domain;
using DockLauncher.Modules.Items.Domain;
using DockLauncher.Modules.LaunchProfiles.Domain;
using DockLauncher.Modules.Panels.Domain;

namespace DockLauncher.Modules.Settings.Domain;

public sealed record AppSettings(
    string Language,
    string Theme,
    bool StartWithWindows,
    string GlobalHotkey);

public sealed record Workspace(
    int SchemaVersion,
    AppSettings Settings,
    IReadOnlyList<Panel> Panels,
    IReadOnlyList<LauncherItem> Items,
    IReadOnlyList<Group> Groups,
    IReadOnlyList<LaunchProfile> LaunchProfiles);
