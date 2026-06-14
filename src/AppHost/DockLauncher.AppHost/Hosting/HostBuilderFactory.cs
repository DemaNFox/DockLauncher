using System.IO;
using DockLauncher.AppHost.Configuration;
using DockLauncher.BuildingBlocks.Infrastructure;
using DockLauncher.BuildingBlocks.Infrastructure.FileSystem;
using DockLauncher.Integrations.Windows;
using DockLauncher.Modules.FolderFlyouts.Infrastructure;
using DockLauncher.Modules.Groups.Infrastructure;
using DockLauncher.Modules.Hotkeys.Infrastructure;
using DockLauncher.Modules.Icons.Infrastructure;
using DockLauncher.Modules.Items.Infrastructure;
using DockLauncher.Modules.LaunchProfiles.Infrastructure;
using DockLauncher.Modules.Panels.Infrastructure;
using DockLauncher.Modules.Settings.Infrastructure;
using DockLauncher.Modules.ShellIntegration.Infrastructure;
using DockLauncher.Modules.Tray.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DockLauncher.AppHost.Hosting;

public static class HostBuilderFactory
{
    public static IHost Build()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);

        var appDataRoot = Environment.GetEnvironmentVariable(AppDataPaths.RootOverrideEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(appDataRoot))
        {
            appDataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DockLauncher");
        }

        var logPath = Path.Combine(appDataRoot, "logs", "docklauncher-.log");

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);

        builder.Services.Configure<ShellOptions>(builder.Configuration.GetSection("Shell"));
        builder.Services.AddBuildingBlocksInfrastructure();
        builder.Services.AddWindowsIntegrations();
        builder.Services.AddPanelsModule();
        builder.Services.AddItemsModule();
        builder.Services.AddSettingsModule();
        builder.Services.AddGroupsModule();
        builder.Services.AddLaunchProfilesModule();
        builder.Services.AddFolderFlyoutsModule();
        builder.Services.AddIconsModule();
        builder.Services.AddTrayModule();
        builder.Services.AddHotkeysModule();
        builder.Services.AddShellIntegrationModule();

        Composition.ServiceCollectionExtensions.AddApplicationShell(builder.Services);

        return builder.Build();
    }
}
