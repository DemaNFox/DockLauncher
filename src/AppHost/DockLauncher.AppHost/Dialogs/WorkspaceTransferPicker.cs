using DockLauncher.BuildingBlocks.Application.Contracts;
using Microsoft.Win32;

namespace DockLauncher.AppHost.Dialogs;

public sealed class WorkspaceTransferPicker : IWorkspaceTransferPicker
{
    public Task<string?> PickImportFileAsync(CancellationToken cancellationToken = default)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import DockLauncher workspace",
            Filter = "DockLauncher workspace|*.json|JSON files|*.json|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    public Task<string?> PickExportFileAsync(CancellationToken cancellationToken = default)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export DockLauncher workspace",
            Filter = "DockLauncher workspace|*.json|JSON files|*.json|All files|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = $"docklauncher-workspace-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }
}
