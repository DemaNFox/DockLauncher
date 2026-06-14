namespace DockLauncher.AppHost.Dialogs;

public interface ITextPromptService
{
    Task<string?> PromptAsync(string title, string message, string initialValue, CancellationToken cancellationToken = default);
}
