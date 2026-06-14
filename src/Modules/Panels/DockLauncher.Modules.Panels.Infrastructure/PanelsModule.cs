using DockLauncher.Modules.Panels.Application;
using DockLauncher.Modules.Settings.Application;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.Modules.Panels.Infrastructure;

public static class PanelsModule
{
    public static IServiceCollection AddPanelsModule(this IServiceCollection services)
    {
        services.AddSingleton<IPanelRepository, WorkspacePanelRepository>();
        services.AddTransient<GetPanelsQueryHandler>();
        return services;
    }
}

internal sealed class WorkspacePanelRepository : IPanelRepository
{
    private readonly IWorkspaceStore _workspaceStore;

    public WorkspacePanelRepository(IWorkspaceStore workspaceStore)
    {
        _workspaceStore = workspaceStore;
    }

    public async Task<IReadOnlyList<Domain.Panel>> GetAllAsync(CancellationToken cancellationToken)
    {
        var workspace = await _workspaceStore.LoadAsync(cancellationToken);
        return workspace.Panels;
    }
}
