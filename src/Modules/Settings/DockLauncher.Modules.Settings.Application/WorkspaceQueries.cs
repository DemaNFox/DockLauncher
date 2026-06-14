using DockLauncher.BuildingBlocks.Application.Abstractions;
using DockLauncher.Modules.Settings.Domain;

namespace DockLauncher.Modules.Settings.Application;

public interface IWorkspaceStore
{
    Task<Workspace> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(Workspace workspace, CancellationToken cancellationToken);
    Task ExportAsync(Workspace workspace, string path, CancellationToken cancellationToken);
    Task<Workspace> ImportAsync(string path, CancellationToken cancellationToken);
    Task<Workspace> ResetAsync(CancellationToken cancellationToken);
}

public sealed record LoadWorkspaceQuery : IQuery<Workspace>;

public sealed class LoadWorkspaceQueryHandler : IQueryHandler<LoadWorkspaceQuery, Workspace>
{
    private readonly IWorkspaceStore _workspaceStore;

    public LoadWorkspaceQueryHandler(IWorkspaceStore workspaceStore)
    {
        _workspaceStore = workspaceStore;
    }

    public Task<Workspace> HandleAsync(LoadWorkspaceQuery query, CancellationToken cancellationToken)
    {
        return _workspaceStore.LoadAsync(cancellationToken);
    }
}

public sealed record SaveWorkspaceCommand(Workspace Workspace) : ICommand<bool>;

public sealed class SaveWorkspaceCommandHandler : ICommandHandler<SaveWorkspaceCommand, bool>
{
    private readonly IWorkspaceStore _workspaceStore;

    public SaveWorkspaceCommandHandler(IWorkspaceStore workspaceStore)
    {
        _workspaceStore = workspaceStore;
    }

    public async Task<bool> HandleAsync(SaveWorkspaceCommand command, CancellationToken cancellationToken)
    {
        await _workspaceStore.SaveAsync(command.Workspace, cancellationToken);
        return true;
    }
}
