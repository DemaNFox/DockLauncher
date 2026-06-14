using DockLauncher.BuildingBlocks.Application.Contracts;
using Microsoft.Win32;

namespace DockLauncher.AppHost.Dialogs;

public sealed class ShellItemTargetPicker : IItemTargetPicker
{
    public Task<string?> PickFileAsync(CancellationToken cancellationToken = default)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select file, shortcut or executable",
            Filter = "All supported files|*.exe;*.lnk;*.bat;*.cmd;*.ps1;*.*|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select folder",
            Multiselect = false
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FolderName : null);
    }
}
