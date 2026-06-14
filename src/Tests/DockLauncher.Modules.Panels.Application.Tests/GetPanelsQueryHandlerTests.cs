using DockLauncher.Modules.Panels.Application;
using DockLauncher.Modules.Panels.Domain;
using FluentAssertions;
using NSubstitute;

namespace DockLauncher.Modules.Panels.Application.Tests;

public class GetPanelsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldReturnPanelsFromRepository()
    {
        var repository = Substitute.For<IPanelRepository>();
        repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(
            new[]
            {
                new Panel(Guid.NewGuid(), "Work", PanelPosition.Bottom, PanelLayoutMode.IconOnly, new PanelAppearance(1, 40, true, true, false))
            });

        var handler = new GetPanelsQueryHandler(repository);

        var result = await handler.HandleAsync(new GetPanelsQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
    }
}