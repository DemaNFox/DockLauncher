using DockLauncher.Modules.Items.Domain;
using FluentAssertions;

namespace DockLauncher.Modules.Items.Domain.Tests;

public class LauncherItemTests
{
    [Fact]
    public void Constructor_ShouldCaptureTargetAndFlags()
    {
        var item = new LauncherItem(Guid.NewGuid(), "Terminal", LauncherItemType.Application, "wt.exe", "-w 0 nt", true);

        item.Target.Should().Be("wt.exe");
        item.RunAsAdministrator.Should().BeTrue();
    }
}