using DockLauncher.Modules.Items.Domain;

namespace DockLauncher.AppHost.Dialogs;

public interface IItemEditorService
{
    Task<ItemEditorResult?> EditAsync(ItemEditorRequest request, CancellationToken cancellationToken = default);
}

public sealed record ItemEditorRequest(
    string DisplayName,
    LauncherItemType Type,
    string Target,
    string? Arguments,
    bool RunAsAdministrator,
    string? IconPath,
    bool TargetEditable,
    string HelperText);

public sealed record ItemEditorResult(
    string DisplayName,
    string Target,
    string? Arguments,
    bool RunAsAdministrator,
    string? IconPath);
