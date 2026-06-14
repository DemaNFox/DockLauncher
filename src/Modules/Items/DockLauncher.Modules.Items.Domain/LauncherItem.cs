using DockLauncher.BuildingBlocks.Domain.Abstractions;
using DockLauncher.BuildingBlocks.Domain.Guards;

namespace DockLauncher.Modules.Items.Domain;

public sealed class LauncherItem : AggregateRoot<Guid>
{
    public LauncherItem(Guid id, string displayName, LauncherItemType type, string target, string? arguments = null, bool runAsAdministrator = false, string? iconPath = null)
        : base(id)
    {
        DisplayName = Guard.AgainstNullOrWhiteSpace(displayName, nameof(displayName));
        Type = type;
        Target = Guard.AgainstNullOrWhiteSpace(target, nameof(target));
        Arguments = arguments;
        RunAsAdministrator = runAsAdministrator;
        IconPath = string.IsNullOrWhiteSpace(iconPath) ? null : iconPath;
    }

    public string DisplayName { get; }
    public LauncherItemType Type { get; }
    public string Target { get; }
    public string? Arguments { get; }
    public bool RunAsAdministrator { get; }
    public string? IconPath { get; }
}

public enum LauncherItemType
{
    Application,
    Shortcut,
    Folder,
    File,
    Url,
    Command,
    Action,
    Separator
}
