namespace DockLauncher.AppHost.Dialogs;

public sealed class ItemEditorService : IItemEditorService
{
    private readonly IRemoteIconCache _remoteIconCache;

    public ItemEditorService(IRemoteIconCache remoteIconCache)
    {
        _remoteIconCache = remoteIconCache;
    }

    public Task<ItemEditorResult?> EditAsync(ItemEditorRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var window = new ItemEditorWindow(request, _remoteIconCache);
        var accepted = window.ShowDialog();
        return Task.FromResult(accepted == true ? window.Result : null);
    }
}
