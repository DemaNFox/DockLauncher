namespace DockLauncher.BuildingBlocks.Application.Contracts;

public interface IDockShellController
{
    Task RefreshAsync(CancellationToken cancellationToken = default);

    Task PreviewWorkspaceAsync(DockLauncher.Modules.Settings.Domain.Workspace workspace, CancellationToken cancellationToken = default);

    Task AddPathsToPanelAsync(Guid panelId, IReadOnlyList<string> paths, CancellationToken cancellationToken = default);

    Task UpdatePanelPositionAsync(
        Guid panelId,
        DockLauncher.Modules.Panels.Domain.PanelPosition position,
        double? floatingLeft = null,
        double? floatingTop = null,
        double? customWidth = null,
        double? customHeight = null,
        CancellationToken cancellationToken = default);

    void TogglePanelsVisibility();

    void EnsurePanelsVisible();

    void ShowHiddenPanels();

    void ShowConfigurator();

    void Exit();
}
