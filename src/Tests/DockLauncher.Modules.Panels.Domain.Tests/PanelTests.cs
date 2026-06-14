using DockLauncher.Modules.Panels.Domain;
using FluentAssertions;

namespace DockLauncher.Modules.Panels.Domain.Tests;

public class PanelTests
{
    [Fact]
    public void AddItem_ShouldIgnoreDuplicates()
    {
        var panel = new Panel(Guid.NewGuid(), "Dev", PanelPosition.Bottom, PanelLayoutMode.Grid, new PanelAppearance(0.9, 32, true, true, false));
        var itemId = Guid.NewGuid();

        panel.AddItem(itemId);
        panel.AddItem(itemId);

        panel.ItemIds.Should().ContainSingle().Which.Should().Be(itemId);
    }
}