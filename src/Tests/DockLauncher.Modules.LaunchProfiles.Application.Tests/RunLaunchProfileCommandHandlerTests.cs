using DockLauncher.Modules.LaunchProfiles.Application;
using DockLauncher.Modules.LaunchProfiles.Domain;
using FluentAssertions;
using NSubstitute;

namespace DockLauncher.Modules.LaunchProfiles.Application.Tests;

public class RunLaunchProfileCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldInvokeRunner()
    {
        var runner = Substitute.For<ILaunchProfileRunner>();
        var handler = new RunLaunchProfileCommandHandler(runner);
        var profile = new LaunchProfile(Guid.NewGuid(), "Dev Start", new[] { new LaunchStep(Guid.NewGuid(), 0, false) });

        var result = await handler.HandleAsync(new RunLaunchProfileCommand(profile), CancellationToken.None);

        await runner.Received(1).RunAsync(profile, CancellationToken.None);
        result.Should().BeTrue();
    }
}