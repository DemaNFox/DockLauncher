namespace DockLauncher.AppHost.Configuration;

public sealed class ShellOptions
{
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "system";
    public bool StartWithWindows { get; set; }
    public string GlobalHotkey { get; set; } = "Alt+Space";
}