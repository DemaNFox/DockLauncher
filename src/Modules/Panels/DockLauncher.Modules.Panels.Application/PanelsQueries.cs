using DockLauncher.BuildingBlocks.Application.Abstractions;
using DockLauncher.Modules.Panels.Domain;

namespace DockLauncher.Modules.Panels.Application;

public interface IPanelRepository
{
    Task<IReadOnlyList<Panel>> GetAllAsync(CancellationToken cancellationToken);
}

public sealed record GetPanelsQuery : IQuery<IReadOnlyList<Panel>>;

public sealed class GetPanelsQueryHandler : IQueryHandler<GetPanelsQuery, IReadOnlyList<Panel>>
{
    private readonly IPanelRepository _repository;

    public GetPanelsQueryHandler(IPanelRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<Panel>> HandleAsync(GetPanelsQuery query, CancellationToken cancellationToken)
    {
        return _repository.GetAllAsync(cancellationToken);
    }
}