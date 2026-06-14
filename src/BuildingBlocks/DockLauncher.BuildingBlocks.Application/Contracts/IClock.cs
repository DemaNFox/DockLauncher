namespace DockLauncher.BuildingBlocks.Application.Contracts;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}