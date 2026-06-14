namespace DockLauncher.BuildingBlocks.Infrastructure.FileSystem;

public sealed class AppDataPaths
{
    public const string RootOverrideEnvironmentVariable = "DOCKLAUNCHER_DATA_ROOT";

    public AppDataPaths()
        : this(GetDefaultRoot())
    {
    }

    public AppDataPaths(string root)
    {
        Root = root;
        WorkspaceFilePath = Path.Combine(root, "workspace.json");
        LogDirectory = Path.Combine(root, "logs");
        IconsDirectory = Path.Combine(root, "icons");
    }

    public string Root { get; }
    public string WorkspaceFilePath { get; }
    public string LogDirectory { get; }
    public string IconsDirectory { get; }

    private static string GetDefaultRoot()
    {
        var overrideRoot = Environment.GetEnvironmentVariable(RootOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideRoot))
        {
            return overrideRoot;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DockLauncher");
    }
}
