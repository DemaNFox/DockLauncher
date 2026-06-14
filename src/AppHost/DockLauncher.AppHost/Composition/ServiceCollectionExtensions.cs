using DockLauncher.AppHost.Docking;
using DockLauncher.AppHost.Dialogs;
using DockLauncher.AppHost.Hotkeys;
using DockLauncher.AppHost.LaunchProfiles;
using DockLauncher.AppHost.Tray;
using DockLauncher.BuildingBlocks.Application.Contracts;
using DockLauncher.BuildingBlocks.Presentation.Wpf;
using DockLauncher.Modules.LaunchProfiles.Application;
using DockLauncher.Modules.Panels.Presentation.Wpf;
using DockLauncher.Modules.Settings.Presentation.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.AppHost.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationShell(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<IDockShellController, DockShellCoordinator>();
        services.AddSingleton<IDockPanelIconProvider, DockPanelIconProvider>();
        services.AddSingleton<IItemIconProvider>(provider => provider.GetRequiredService<IDockPanelIconProvider>());
        services.AddSingleton<IItemTargetPicker, ShellItemTargetPicker>();
        services.AddSingleton<IRemoteIconCache, RemoteIconCache>();
        services.AddSingleton<IItemEditorService, ItemEditorService>();
        services.AddSingleton<IPanelColorPicker, PanelColorPicker>();
        services.AddSingleton<IWorkspaceTransferPicker, WorkspaceTransferPicker>();
        services.AddSingleton<ITextPromptService, TextPromptService>();
        services.AddSingleton<ILaunchProfileRunner, WorkspaceLaunchProfileRunner>();
        services.AddSingleton<DockGlobalHotkey>();
        services.AddSingleton<DockTrayIcon>();
        services.AddTransient<PanelsOverviewViewModel>();
        services.AddTransient<WorkspaceEditorViewModel>();
        return services;
    }
}
