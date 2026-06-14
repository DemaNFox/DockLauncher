using DockLauncher.BuildingBlocks.Application.Contracts;
using DockLauncher.BuildingBlocks.Infrastructure.Serialization;
using DockLauncher.BuildingBlocks.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBuildingBlocksInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IJsonSerializer, SystemTextJsonSerializer>();
        services.AddSingleton<FileSystem.AppDataPaths>();
        return services;
    }
}