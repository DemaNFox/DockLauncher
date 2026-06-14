using CommunityToolkit.Mvvm.ComponentModel;
using DockLauncher.BuildingBlocks.Presentation.Wpf;
using DockLauncher.Modules.Panels.Application;

namespace DockLauncher.Modules.Panels.Presentation.Wpf;

public sealed partial class PanelsOverviewViewModel : ViewModelBase
{
    private readonly GetPanelsQueryHandler _handler;

    public PanelsOverviewViewModel(GetPanelsQueryHandler handler)
    {
        _handler = handler;
    }

    [ObservableProperty]
    private IReadOnlyList<PanelTileViewModel> panels = [];

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await _handler.HandleAsync(new GetPanelsQuery(), cancellationToken);
        Panels = result
            .Select(panel => new PanelTileViewModel(panel.Name, panel.Position.ToString(), panel.LayoutMode.ToString()))
            .ToArray();
    }
}

public sealed record PanelTileViewModel(string Name, string Position, string LayoutMode);