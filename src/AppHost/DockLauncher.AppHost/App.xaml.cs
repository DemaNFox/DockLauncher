using System.IO;
using System.Windows;
using DockLauncher.BuildingBlocks.Application.Contracts;
using DockLauncher.BuildingBlocks.Infrastructure.FileSystem;
using DockLauncher.AppHost.Hosting;
using DockLauncher.AppHost.Hotkeys;
using DockLauncher.AppHost.Tray;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DockLauncher.AppHost;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _host = HostBuilderFactory.Build();
        await _host.StartAsync();
        await _host.Services.GetRequiredService<DockGlobalHotkey>().InitializeAsync();
        _host.Services.GetRequiredService<DockTrayIcon>().Initialize();
        var dockShellController = _host.Services.GetRequiredService<IDockShellController>();
        await dockShellController.RefreshAsync();

        ShowConfiguratorOnFirstRun(_host.Services, dockShellController);
    }

    private static void ShowConfiguratorOnFirstRun(IServiceProvider services, IDockShellController dockShellController)
    {
        var paths = services.GetRequiredService<AppDataPaths>();
        var markerPath = Path.Combine(paths.Root, "first-run-complete.txt");
        if (File.Exists(markerPath))
        {
            return;
        }

        Directory.CreateDirectory(paths.Root);
        File.WriteAllText(markerPath, DateTimeOffset.UtcNow.ToString("O"));
        dockShellController.ShowConfigurator();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
