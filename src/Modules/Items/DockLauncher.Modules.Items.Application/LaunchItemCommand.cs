using DockLauncher.BuildingBlocks.Application.Abstractions;
using DockLauncher.BuildingBlocks.Domain.Results;
using DockLauncher.Modules.Items.Domain;

namespace DockLauncher.Modules.Items.Application;

public interface ILauncherItemService
{
    Task<Result> LaunchAsync(LauncherItem item, CancellationToken cancellationToken);
}

public sealed record LaunchItemCommand(LauncherItem Item) : ICommand<Result>;

public sealed class LaunchItemCommandHandler : ICommandHandler<LaunchItemCommand, Result>
{
    private readonly ILauncherItemService _service;

    public LaunchItemCommandHandler(ILauncherItemService service)
    {
        _service = service;
    }

    public Task<Result> HandleAsync(LaunchItemCommand command, CancellationToken cancellationToken)
    {
        return _service.LaunchAsync(command.Item, cancellationToken);
    }
}