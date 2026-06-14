using DockLauncher.Modules.Items.Application;
using DockLauncher.Modules.Items.Domain;
using DockLauncher.Modules.LaunchProfiles.Application;
using DockLauncher.Modules.Settings.Application;

namespace DockLauncher.AppHost.LaunchProfiles;

public sealed class WorkspaceLaunchProfileRunner : ILaunchProfileRunner
{
    private readonly IWorkspaceStore _workspaceStore;
    private readonly LaunchItemCommandHandler _launchItemCommandHandler;

    public WorkspaceLaunchProfileRunner(
        IWorkspaceStore workspaceStore,
        LaunchItemCommandHandler launchItemCommandHandler)
    {
        _workspaceStore = workspaceStore;
        _launchItemCommandHandler = launchItemCommandHandler;
    }

    public async Task RunAsync(Modules.LaunchProfiles.Domain.LaunchProfile profile, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceStore.LoadAsync(cancellationToken);
        var itemLookup = workspace.Items.ToDictionary(item => item.Id);

        foreach (var step in profile.Steps)
        {
            if (step.DelayMs > 0)
            {
                await Task.Delay(step.DelayMs, cancellationToken);
            }

            if (!itemLookup.TryGetValue(step.ItemId, out var item))
            {
                continue;
            }

            var launcherItem = step.RunAsAdministrator == item.RunAsAdministrator
                ? item
                : new LauncherItem(item.Id, item.DisplayName, item.Type, item.Target, item.Arguments, step.RunAsAdministrator);

            await _launchItemCommandHandler.HandleAsync(new LaunchItemCommand(launcherItem), cancellationToken);
        }
    }
}
