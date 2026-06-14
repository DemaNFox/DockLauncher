using System.Diagnostics;
using DockLauncher.BuildingBlocks.Domain.Results;
using DockLauncher.Modules.Items.Application;
using DockLauncher.Modules.Items.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.Integrations.Windows;

public static class DependencyInjection
{
    public static IServiceCollection AddWindowsIntegrations(this IServiceCollection services)
    {
        services.AddSingleton<ILauncherItemService, WindowsLauncherItemService>();
        return services;
    }
}

internal sealed class WindowsLauncherItemService : ILauncherItemService
{
    public Task<Result> LaunchAsync(LauncherItem item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.Target))
        {
            return Task.FromResult(Result.Failure(new Error("items.target.empty", "Launcher item target is missing.")));
        }

        if (BuiltInDockActions.TryResolve(item, out var resolvedAction))
        {
            var actionStartInfo = new ProcessStartInfo
            {
                FileName = resolvedAction.FileName,
                Arguments = resolvedAction.Arguments,
                UseShellExecute = true,
                Verb = item.RunAsAdministrator ? "runas" : string.Empty
            };

            return StartProcess(actionStartInfo);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = item.Target,
            Arguments = item.Arguments ?? string.Empty,
            UseShellExecute = true,
            Verb = item.RunAsAdministrator ? "runas" : string.Empty
        };

        return StartProcess(startInfo);
    }

    private static Task<Result> StartProcess(ProcessStartInfo startInfo)
    {
        try
        {
            var process = Process.Start(startInfo);
            return Task.FromResult(process is null
                ? Result.Failure(new Error("items.launch.failed", "Process start returned no process instance."))
                : Result.Success());
        }
        catch (Exception exception)
        {
            return Task.FromResult(Result.Failure(new Error("items.launch.exception", exception.Message)));
        }
    }
}
