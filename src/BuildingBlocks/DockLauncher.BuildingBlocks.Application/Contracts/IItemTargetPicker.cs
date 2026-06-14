namespace DockLauncher.BuildingBlocks.Application.Contracts;

public interface IItemTargetPicker
{
    Task<string?> PickFileAsync(CancellationToken cancellationToken = default);

    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);
}

public interface IWorkspaceTransferPicker
{
    Task<string?> PickImportFileAsync(CancellationToken cancellationToken = default);

    Task<string?> PickExportFileAsync(CancellationToken cancellationToken = default);
}

public interface IPanelColorPicker
{
    Task<string?> PickColorAsync(string? currentColor, CancellationToken cancellationToken = default);
}
