using DockLauncher.Modules.Items.Application;
using DockLauncher.Modules.Items.Domain;
using DockLauncher.Modules.Panels.Application;
using DockLauncher.Modules.Panels.Domain;
using DockLauncher.Modules.Panels.Infrastructure;
using FluentAssertions;

namespace DockLauncher.Architecture.Tests;

public class DependencyRulesTests
{
    [Fact]
    public void PanelsDomain_ShouldNotReferenceInfrastructureAssembly()
    {
        var domainAssembly = typeof(Panel).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

        domainAssembly.Should().NotContain(typeof(PanelsModule).Assembly.GetName().Name);
    }

    [Fact]
    public void PanelsApplication_ShouldReferencePanelsDomain()
    {
        var applicationAssembly = typeof(GetPanelsQueryHandler).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

        applicationAssembly.Should().Contain(typeof(Panel).Assembly.GetName().Name);
    }

    [Fact]
    public void ItemsApplication_ShouldReferenceItemsDomain()
    {
        var applicationAssembly = typeof(LaunchItemCommandHandler).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

        applicationAssembly.Should().Contain(typeof(LauncherItem).Assembly.GetName().Name);
    }
}