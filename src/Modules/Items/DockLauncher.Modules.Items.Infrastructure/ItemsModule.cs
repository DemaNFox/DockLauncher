using DockLauncher.Modules.Items.Application;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.Modules.Items.Infrastructure;

public static class ItemsModule
{
    public static IServiceCollection AddItemsModule(this IServiceCollection services)
    {
        services.AddTransient<LaunchItemCommandHandler>();
        return services;
    }
}