namespace DockLauncher.AppHost.Dialogs;

public sealed class TextPromptService : ITextPromptService
{
    public Task<string?> PromptAsync(string title, string message, string initialValue, CancellationToken cancellationToken = default)
    {
        var window = new TextPromptWindow(title, message, initialValue);
        var accepted = window.ShowDialog() == true;
        return Task.FromResult(accepted ? window.ResponseText : null);
    }
}
