using DockLauncher.Modules.LaunchProfiles.Application;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.Modules.LaunchProfiles.Infrastructure;

public static class LaunchProfilesModule
{
    public static IServiceCollection AddLaunchProfilesModule(this IServiceCollection services)
    {
        services.AddSingleton<ILaunchProfileRunner, DelayedLaunchProfileRunner>();
        services.AddTransient<RunLaunchProfileCommandHandler>();
        return services;
    }
}

internal sealed class DelayedLaunchProfileRunner : ILaunchProfileRunner
{
    public async Task RunAsync(Domain.LaunchProfile profile, CancellationToken cancellationToken)
    {
        foreach (var step in profile.Steps)
        {
            if (step.DelayMs > 0)
            {
                await Task.Delay(step.DelayMs, cancellationToken);
            }
        }
    }
}