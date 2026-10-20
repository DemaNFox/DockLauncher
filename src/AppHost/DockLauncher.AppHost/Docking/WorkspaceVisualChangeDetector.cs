using DockLauncher.Modules.Groups.Domain;
using DockLauncher.Modules.Items.Domain;
using DockLauncher.Modules.LaunchProfiles.Domain;
using DockLauncher.Modules.Panels.Domain;
using DockLauncher.Modules.Settings.Domain;

namespace DockLauncher.AppHost.Docking;

public static class WorkspaceVisualChangeDetector
{
    public static IReadOnlySet<Guid> PanelsToRefresh(Workspace? previous, Workspace current, bool forceAll)
    {
        if (forceAll || previous is null || HasSharedContentChanges(previous, current))
        {
            return current.Panels.Select(panel => panel.Id).ToHashSet();
        }

        return previous.Panels.Zip(current.Panels)
            .Where(pair => !PanelEquals(pair.First, pair.Second))
            .Select(pair => pair.Second.Id)
            .ToHashSet();
    }

    public static bool ShouldCloseFlyouts(Workspace? previous, Workspace current, bool forceAll)
    {
        if (forceAll || previous is null || HasSharedContentChanges(previous, current))
        {
            return true;
        }

        return previous.Panels.Zip(current.Panels).Any(pair =>
            pair.First.Position != pair.Second.Position
            || pair.First.LayoutMode != pair.Second.LayoutMode
            || !pair.First.ItemIds.SequenceEqual(pair.Second.ItemIds));
    }

    private static bool HasSharedContentChanges(Workspace previous, Workspace current)
    {
        return previous.Panels.Count != current.Panels.Count
            || previous.Panels.Zip(current.Panels).Any(pair =>
                pair.First.Id != pair.Second.Id || pair.First.Name != pair.Second.Name)
            || !SequenceEquals(previous.Items, current.Items, ItemEquals)
            || !SequenceEquals(previous.Groups, current.Groups, GroupEquals)
            || !SequenceEquals(previous.LaunchProfiles, current.LaunchProfiles, ProfileEquals);
    }

    private static bool PanelEquals(Panel first, Panel second) =>
        first.Id == second.Id
        && first.Name == second.Name
        && first.Position == second.Position
        && first.LayoutMode == second.LayoutMode
        && first.Appearance == second.Appearance
        && first.ItemIds.SequenceEqual(second.ItemIds);

    private static bool ItemEquals(LauncherItem first, LauncherItem second) =>
        first.Id == second.Id
        && first.DisplayName == second.DisplayName
        && first.Type == second.Type
        && first.Target == second.Target
        && first.Arguments == second.Arguments
        && first.RunAsAdministrator == second.RunAsAdministrator
        && first.IconPath == second.IconPath;

    private static bool GroupEquals(Group first, Group second) =>
        first.Id == second.Id
        && first.Name == second.Name
        && first.ItemIds.SequenceEqual(second.ItemIds);

    private static bool ProfileEquals(LaunchProfile first, LaunchProfile second) =>
        first.Id == second.Id
        && first.Name == second.Name
        && first.Steps.SequenceEqual(second.Steps);

    private static bool SequenceEquals<T>(IReadOnlyList<T> first, IReadOnlyList<T> second, Func<T, T, bool> equals) =>
        first.Count == second.Count && first.Zip(second).All(pair => equals(pair.First, pair.Second));
}
