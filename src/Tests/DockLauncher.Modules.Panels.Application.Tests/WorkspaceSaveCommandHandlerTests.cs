using DockLauncher.Modules.Settings.Application;
using DockLauncher.Modules.Settings.Domain;
using FluentAssertions;
using NSubstitute;

namespace DockLauncher.Modules.Panels.Application.Tests;

public class WorkspaceSaveCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldPersistWorkspace()
    {
        var store = Substitute.For<IWorkspaceStore>();
        var handler = new SaveWorkspaceCommandHandler(store);
        var workspace = new Workspace(1, new AppSettings("en", "system", false, "Alt+Space"), [], [], [], []);

        var result = await handler.HandleAsync(new SaveWorkspaceCommand(workspace), CancellationToken.None);

        result.Should().BeTrue();
        await store.Received(1).SaveAsync(workspace, CancellationToken.None);
    }
}
