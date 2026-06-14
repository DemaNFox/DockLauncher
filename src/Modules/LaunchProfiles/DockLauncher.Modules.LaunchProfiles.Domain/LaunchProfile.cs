namespace DockLauncher.Modules.LaunchProfiles.Domain;

public sealed record LaunchProfile(Guid Id, string Name, IReadOnlyList<LaunchStep> Steps);

public sealed record LaunchStep(Guid ItemId, int DelayMs, bool RunAsAdministrator);