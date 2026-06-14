using DockLauncher.BuildingBlocks.Application.Contracts;

namespace DockLauncher.BuildingBlocks.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}