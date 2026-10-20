using DockLauncher.AppHost.Docking;
using DockLauncher.Modules.Items.Domain;
using DockLauncher.Modules.Panels.Domain;
using DockLauncher.Modules.Settings.Domain;
using FluentAssertions;

namespace DockLauncher.UiSmoke.Tests;

public class WorkspaceVisualChangeDetectorTests
{
    [Fact]
    public void AppearanceEdit_ShouldRefreshOnlyEditedPanelAndPreserveFlyout()
    {
        var first = CreatePanel("First");
        var second = CreatePanel("Second");
        var previous = CreateWorkspace([first, second]);
        var changedFirst = new Panel(first.Id, first.Name, first.Position, first.LayoutMode,
            first.Appearance with { Opacity = 0.6 });
        var current = CreateWorkspace([changedFirst, second]);

        WorkspaceVisualChangeDetector.PanelsToRefresh(previous, current, forceAll: false)
            .Should().BeEquivalentTo([first.Id]);
        WorkspaceVisualChangeDetector.ShouldCloseFlyouts(previous, current, forceAll: false)
            .Should().BeFalse();
    }

    [Fact]
    public void ItemEdit_ShouldRefreshEveryPanelAndCloseFlyout()
    {
        var first = CreatePanel("First");
        var second = CreatePanel("Second");
        var itemId = Guid.NewGuid();
        var previous = CreateWorkspace([first, second], [new LauncherItem(itemId, "Old", LauncherItemType.Application, "notepad.exe")]);
        var current = CreateWorkspace([first, second], [new LauncherItem(itemId, "New", LauncherItemType.Application, "notepad.exe")]);

        WorkspaceVisualChangeDetector.PanelsToRefresh(previous, current, forceAll: false)
            .Should().BeEquivalentTo([first.Id, second.Id]);
        WorkspaceVisualChangeDetector.ShouldCloseFlyouts(previous, current, forceAll: false)
            .Should().BeTrue();
    }

    private static Panel CreatePanel(string name) =>
        new(Guid.NewGuid(), name, PanelPosition.Bottom, PanelLayoutMode.IconOnly,
            new PanelAppearance(0.9, 40, true, false, false));

    private static Workspace CreateWorkspace(IReadOnlyList<Panel> panels, IReadOnlyList<LauncherItem>? items = null) =>
        new(1, new AppSettings("en", "system", false, "Alt+Space"), panels, items ?? [], [], []);
}
