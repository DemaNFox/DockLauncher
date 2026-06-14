using DockLauncher.BuildingBlocks.Application.Abstractions;
using DockLauncher.Modules.LaunchProfiles.Domain;

namespace DockLauncher.Modules.LaunchProfiles.Application;

public interface ILaunchProfileRunner
{
    Task RunAsync(LaunchProfile profile, CancellationToken cancellationToken);
}

public sealed record RunLaunchProfileCommand(LaunchProfile Profile) : ICommand<bool>;

public sealed class RunLaunchProfileCommandHandler : ICommandHandler<RunLaunchProfileCommand, bool>
{
    private readonly ILaunchProfileRunner _runner;

    public RunLaunchProfileCommandHandler(ILaunchProfileRunner runner)
    {
        _runner = runner;
    }

    public async Task<bool> HandleAsync(RunLaunchProfileCommand command, CancellationToken cancellationToken)
    {
        await _runner.RunAsync(command.Profile, cancellationToken);
        return true;
    }
}