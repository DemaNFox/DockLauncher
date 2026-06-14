$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$srcRoot = Join-Path $root "src"
$solutionPath = Join-Path $srcRoot "DockLauncher.sln"

function Write-Utf8File {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $normalized = $Content -replace "`r?`n", "`r`n"
    [System.IO.File]::WriteAllText($Path, $normalized, [System.Text.UTF8Encoding]::new($false))
}

function New-ProjectDefinition {
    param(
        [string]$Name,
        [string]$RelativePath,
        [string]$Sdk,
        [string]$TargetFramework,
        [bool]$UseWpf = $false,
        [string[]]$ProjectReferences = @(),
        [string[]]$PackageReferences = @(),
        [string[]]$AdditionalProperties = @()
    )

    [PSCustomObject]@{
        Name = $Name
        RelativePath = $RelativePath
        Sdk = $Sdk
        TargetFramework = $TargetFramework
        UseWpf = $UseWpf
        ProjectReferences = $ProjectReferences
        PackageReferences = $PackageReferences
        AdditionalProperties = $AdditionalProperties
        Guid = [guid]::NewGuid().ToString().ToUpper()
    }
}

function Get-CsProjContent {
    param([Parameter(Mandatory = $true)]$Project)

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("<Project Sdk=`"$($Project.Sdk)`">")
    $lines.Add("  <PropertyGroup>")
    $lines.Add("    <TargetFramework>$($Project.TargetFramework)</TargetFramework>")
    $lines.Add("    <ImplicitUsings>enable</ImplicitUsings>")
    $lines.Add("    <Nullable>enable</Nullable>")
    if ($Project.UseWpf) {
        $lines.Add("    <UseWPF>true</UseWPF>")
    }
    foreach ($property in $Project.AdditionalProperties) {
        $lines.Add("    $property")
    }
    $lines.Add("  </PropertyGroup>")

    if ($Project.PackageReferences.Count -gt 0) {
        $lines.Add("  <ItemGroup>")
        foreach ($reference in $Project.PackageReferences) {
            $lines.Add("    <PackageReference Include=`"$reference`" />")
        }
        $lines.Add("  </ItemGroup>")
    }

    if ($Project.ProjectReferences.Count -gt 0) {
        $lines.Add("  <ItemGroup>")
        foreach ($reference in $Project.ProjectReferences) {
            $lines.Add("    <ProjectReference Include=`"$reference`" />")
        }
        $lines.Add("  </ItemGroup>")
    }

    $lines.Add("</Project>")
    return ($lines -join "`n")
}

$projectTypeGuid = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"
$solutionFolders = @(
    @{ Name = "AppHost"; Guid = [guid]::NewGuid().ToString().ToUpper() },
    @{ Name = "BuildingBlocks"; Guid = [guid]::NewGuid().ToString().ToUpper() },
    @{ Name = "Modules"; Guid = [guid]::NewGuid().ToString().ToUpper() },
    @{ Name = "Integrations"; Guid = [guid]::NewGuid().ToString().ToUpper() },
    @{ Name = "Tests"; Guid = [guid]::NewGuid().ToString().ToUpper() },
    @{ Name = "docs"; Guid = [guid]::NewGuid().ToString().ToUpper() },
    @{ Name = "tools"; Guid = [guid]::NewGuid().ToString().ToUpper() },
    @{ Name = "eng"; Guid = [guid]::NewGuid().ToString().ToUpper() }
)

$projects = [System.Collections.Generic.List[object]]::new()

$projects.Add((New-ProjectDefinition -Name "DockLauncher.BuildingBlocks.Domain" -RelativePath "BuildingBlocks/DockLauncher.BuildingBlocks.Domain/DockLauncher.BuildingBlocks.Domain.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0"))
$projects.Add((New-ProjectDefinition -Name "DockLauncher.BuildingBlocks.Application" -RelativePath "BuildingBlocks/DockLauncher.BuildingBlocks.Application/DockLauncher.BuildingBlocks.Application.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0" -ProjectReferences @("..\DockLauncher.BuildingBlocks.Domain\DockLauncher.BuildingBlocks.Domain.csproj")))
$projects.Add((New-ProjectDefinition -Name "DockLauncher.BuildingBlocks.Infrastructure" -RelativePath "BuildingBlocks/DockLauncher.BuildingBlocks.Infrastructure/DockLauncher.BuildingBlocks.Infrastructure.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0" -ProjectReferences @("..\DockLauncher.BuildingBlocks.Application\DockLauncher.BuildingBlocks.Application.csproj", "..\DockLauncher.BuildingBlocks.Domain\DockLauncher.BuildingBlocks.Domain.csproj") -PackageReferences @("System.IO.Abstractions")))
$projects.Add((New-ProjectDefinition -Name "DockLauncher.BuildingBlocks.Presentation.Wpf" -RelativePath "BuildingBlocks/DockLauncher.BuildingBlocks.Presentation.Wpf/DockLauncher.BuildingBlocks.Presentation.Wpf.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0-windows" -UseWpf $true -ProjectReferences @("..\DockLauncher.BuildingBlocks.Application\DockLauncher.BuildingBlocks.Application.csproj") -PackageReferences @("CommunityToolkit.Mvvm")))

$projects.Add((New-ProjectDefinition -Name "DockLauncher.Integrations.Windows" -RelativePath "Integrations/DockLauncher.Integrations.Windows/DockLauncher.Integrations.Windows.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0-windows" -ProjectReferences @("..\..\BuildingBlocks\DockLauncher.BuildingBlocks.Application\DockLauncher.BuildingBlocks.Application.csproj", "..\..\BuildingBlocks\DockLauncher.BuildingBlocks.Infrastructure\DockLauncher.BuildingBlocks.Infrastructure.csproj", "..\..\Modules\Items\DockLauncher.Modules.Items.Application\DockLauncher.Modules.Items.Application.csproj") -AdditionalProperties @("<AllowUnsafeBlocks>false</AllowUnsafeBlocks>")))

$moduleNames = @("Panels", "Items", "Groups", "LaunchProfiles", "FolderFlyouts", "Settings", "Icons", "Tray", "Hotkeys", "ShellIntegration")

foreach ($module in $moduleNames) {
    $moduleRoot = "Modules/$module"
    $domainName = "DockLauncher.Modules.$module.Domain"
    $applicationName = "DockLauncher.Modules.$module.Application"
    $infrastructureName = "DockLauncher.Modules.$module.Infrastructure"
    $presentationName = "DockLauncher.Modules.$module.Presentation.Wpf"

    $domainReferences = @("..\..\..\BuildingBlocks\DockLauncher.BuildingBlocks.Domain\DockLauncher.BuildingBlocks.Domain.csproj")
    if ($module -eq "Settings") {
        $domainReferences += @(
            "..\..\Panels\DockLauncher.Modules.Panels.Domain\DockLauncher.Modules.Panels.Domain.csproj",
            "..\..\Items\DockLauncher.Modules.Items.Domain\DockLauncher.Modules.Items.Domain.csproj"
        )
    }

    $projects.Add((New-ProjectDefinition -Name $domainName -RelativePath "$moduleRoot/$domainName/$domainName.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0" -ProjectReferences $domainReferences))
    $projects.Add((New-ProjectDefinition -Name $applicationName -RelativePath "$moduleRoot/$applicationName/$applicationName.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0" -ProjectReferences @("..\$domainName\$domainName.csproj", "..\..\..\BuildingBlocks\DockLauncher.BuildingBlocks.Application\DockLauncher.BuildingBlocks.Application.csproj", "..\..\..\BuildingBlocks\DockLauncher.BuildingBlocks.Domain\DockLauncher.BuildingBlocks.Domain.csproj")))
    $projects.Add((New-ProjectDefinition -Name $infrastructureName -RelativePath "$moduleRoot/$infrastructureName/$infrastructureName.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0" -ProjectReferences @("..\$applicationName\$applicationName.csproj", "..\$domainName\$domainName.csproj", "..\..\..\BuildingBlocks\DockLauncher.BuildingBlocks.Infrastructure\DockLauncher.BuildingBlocks.Infrastructure.csproj", "..\..\..\BuildingBlocks\DockLauncher.BuildingBlocks.Application\DockLauncher.BuildingBlocks.Application.csproj")))
    $presentationReferences = @("..\$applicationName\$applicationName.csproj", "..\..\..\BuildingBlocks\DockLauncher.BuildingBlocks.Presentation.Wpf\DockLauncher.BuildingBlocks.Presentation.Wpf.csproj")
    if ($module -eq "Settings") {
        $presentationReferences = @("..\..\Items\DockLauncher.Modules.Items.Application\DockLauncher.Modules.Items.Application.csproj") + $presentationReferences
    }

    $projects.Add((New-ProjectDefinition -Name $presentationName -RelativePath "$moduleRoot/$presentationName/$presentationName.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0-windows" -UseWpf $true -ProjectReferences $presentationReferences -PackageReferences @("CommunityToolkit.Mvvm")))
}

$projects.Add((New-ProjectDefinition -Name "DockLauncher.AppHost" -RelativePath "AppHost/DockLauncher.AppHost/DockLauncher.AppHost.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0-windows" -UseWpf $true -ProjectReferences @(
    "..\..\BuildingBlocks\DockLauncher.BuildingBlocks.Application\DockLauncher.BuildingBlocks.Application.csproj",
    "..\..\BuildingBlocks\DockLauncher.BuildingBlocks.Infrastructure\DockLauncher.BuildingBlocks.Infrastructure.csproj",
    "..\..\BuildingBlocks\DockLauncher.BuildingBlocks.Presentation.Wpf\DockLauncher.BuildingBlocks.Presentation.Wpf.csproj",
    "..\..\Integrations\DockLauncher.Integrations.Windows\DockLauncher.Integrations.Windows.csproj",
    "..\..\Modules\Panels\DockLauncher.Modules.Panels.Application\DockLauncher.Modules.Panels.Application.csproj",
    "..\..\Modules\Panels\DockLauncher.Modules.Panels.Infrastructure\DockLauncher.Modules.Panels.Infrastructure.csproj",
    "..\..\Modules\Panels\DockLauncher.Modules.Panels.Presentation.Wpf\DockLauncher.Modules.Panels.Presentation.Wpf.csproj",
    "..\..\Modules\Items\DockLauncher.Modules.Items.Application\DockLauncher.Modules.Items.Application.csproj",
    "..\..\Modules\Items\DockLauncher.Modules.Items.Infrastructure\DockLauncher.Modules.Items.Infrastructure.csproj",
    "..\..\Modules\Items\DockLauncher.Modules.Items.Presentation.Wpf\DockLauncher.Modules.Items.Presentation.Wpf.csproj",
    "..\..\Modules\Settings\DockLauncher.Modules.Settings.Application\DockLauncher.Modules.Settings.Application.csproj",
    "..\..\Modules\Settings\DockLauncher.Modules.Settings.Infrastructure\DockLauncher.Modules.Settings.Infrastructure.csproj",
    "..\..\Modules\Settings\DockLauncher.Modules.Settings.Presentation.Wpf\DockLauncher.Modules.Settings.Presentation.Wpf.csproj",
    "..\..\Modules\Groups\DockLauncher.Modules.Groups.Infrastructure\DockLauncher.Modules.Groups.Infrastructure.csproj",
    "..\..\Modules\LaunchProfiles\DockLauncher.Modules.LaunchProfiles.Infrastructure\DockLauncher.Modules.LaunchProfiles.Infrastructure.csproj",
    "..\..\Modules\FolderFlyouts\DockLauncher.Modules.FolderFlyouts.Infrastructure\DockLauncher.Modules.FolderFlyouts.Infrastructure.csproj",
    "..\..\Modules\Icons\DockLauncher.Modules.Icons.Infrastructure\DockLauncher.Modules.Icons.Infrastructure.csproj",
    "..\..\Modules\Tray\DockLauncher.Modules.Tray.Infrastructure\DockLauncher.Modules.Tray.Infrastructure.csproj",
    "..\..\Modules\Hotkeys\DockLauncher.Modules.Hotkeys.Infrastructure\DockLauncher.Modules.Hotkeys.Infrastructure.csproj",
    "..\..\Modules\ShellIntegration\DockLauncher.Modules.ShellIntegration.Infrastructure\DockLauncher.Modules.ShellIntegration.Infrastructure.csproj"
) -PackageReferences @("CommunityToolkit.Mvvm", "gong-wpf-dragdrop", "H.NotifyIcon.Wpf", "Microsoft.Extensions.Hosting", "Microsoft.Extensions.Configuration.Json", "Microsoft.Extensions.Options.ConfigurationExtensions", "Serilog.Extensions.Logging", "Serilog.Sinks.File") -AdditionalProperties @("<OutputType>WinExe</OutputType>", "<StartupObject>DockLauncher.AppHost.Program</StartupObject>")))

$testPackages = @(
    "Microsoft.NET.Test.Sdk",
    "xunit",
    "xunit.runner.visualstudio",
    "FluentAssertions",
    "coverlet.collector"
)

$projects.Add((New-ProjectDefinition -Name "DockLauncher.BuildingBlocks.Domain.Tests" -RelativePath "Tests/DockLauncher.BuildingBlocks.Domain.Tests/DockLauncher.BuildingBlocks.Domain.Tests.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0" -ProjectReferences @("..\..\BuildingBlocks\DockLauncher.BuildingBlocks.Domain\DockLauncher.BuildingBlocks.Domain.csproj") -PackageReferences $testPackages))
$projects.Add((New-ProjectDefinition -Name "DockLauncher.Modules.Panels.Domain.Tests" -RelativePath "Tests/DockLauncher.Modules.Panels.Domain.Tests/DockLauncher.Modules.Panels.Domain.Tests.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0" -ProjectReferences @("..\..\Modules\Panels\DockLauncher.Modules.Panels.Domain\DockLauncher.Modules.Panels.Domain.csproj") -PackageReferences $testPackages))
$projects.Add((New-ProjectDefinition -Name "DockLauncher.Modules.Panels.Application.Tests" -RelativePath "Tests/DockLauncher.Modules.Panels.Application.Tests/DockLauncher.Modules.Panels.Application.Tests.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0" -ProjectReferences @("..\..\Modules\Panels\DockLauncher.Modules.Panels.Application\DockLauncher.Modules.Panels.Application.csproj", "..\..\Modules\Panels\DockLauncher.Modules.Panels.Domain\DockLauncher.Modules.Panels.Domain.csproj") -PackageReferences ($testPackages + "NSubstitute")))
$projects.Add((New-ProjectDefinition -Name "DockLauncher.Modules.Items.Domain.Tests" -RelativePath "Tests/DockLauncher.Modules.Items.Domain.Tests/DockLauncher.Modules.Items.Domain.Tests.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0" -ProjectReferences @("..\..\Modules\Items\DockLauncher.Modules.Items.Domain\DockLauncher.Modules.Items.Domain.csproj") -PackageReferences $testPackages))
$projects.Add((New-ProjectDefinition -Name "DockLauncher.Modules.LaunchProfiles.Application.Tests" -RelativePath "Tests/DockLauncher.Modules.LaunchProfiles.Application.Tests/DockLauncher.Modules.LaunchProfiles.Application.Tests.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0" -ProjectReferences @("..\..\Modules\LaunchProfiles\DockLauncher.Modules.LaunchProfiles.Application\DockLauncher.Modules.LaunchProfiles.Application.csproj", "..\..\Modules\LaunchProfiles\DockLauncher.Modules.LaunchProfiles.Domain\DockLauncher.Modules.LaunchProfiles.Domain.csproj") -PackageReferences ($testPackages + "NSubstitute")))
$projects.Add((New-ProjectDefinition -Name "DockLauncher.Architecture.Tests" -RelativePath "Tests/DockLauncher.Architecture.Tests/DockLauncher.Architecture.Tests.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0" -ProjectReferences @(
    "..\..\BuildingBlocks\DockLauncher.BuildingBlocks.Domain\DockLauncher.BuildingBlocks.Domain.csproj",
    "..\..\Modules\Panels\DockLauncher.Modules.Panels.Application\DockLauncher.Modules.Panels.Application.csproj",
    "..\..\Modules\Panels\DockLauncher.Modules.Panels.Domain\DockLauncher.Modules.Panels.Domain.csproj",
    "..\..\Modules\Panels\DockLauncher.Modules.Panels.Infrastructure\DockLauncher.Modules.Panels.Infrastructure.csproj",
    "..\..\Modules\Items\DockLauncher.Modules.Items.Application\DockLauncher.Modules.Items.Application.csproj",
    "..\..\Modules\Items\DockLauncher.Modules.Items.Domain\DockLauncher.Modules.Items.Domain.csproj",
    "..\..\Modules\Items\DockLauncher.Modules.Items.Infrastructure\DockLauncher.Modules.Items.Infrastructure.csproj",
    "..\..\Modules\Settings\DockLauncher.Modules.Settings.Application\DockLauncher.Modules.Settings.Application.csproj",
    "..\..\Modules\Settings\DockLauncher.Modules.Settings.Domain\DockLauncher.Modules.Settings.Domain.csproj",
    "..\..\Modules\Settings\DockLauncher.Modules.Settings.Infrastructure\DockLauncher.Modules.Settings.Infrastructure.csproj"
) -PackageReferences $testPackages))
$projects.Add((New-ProjectDefinition -Name "DockLauncher.Integration.Tests" -RelativePath "Tests/DockLauncher.Integration.Tests/DockLauncher.Integration.Tests.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0" -ProjectReferences @("..\..\Modules\Settings\DockLauncher.Modules.Settings.Infrastructure\DockLauncher.Modules.Settings.Infrastructure.csproj", "..\..\Modules\Settings\DockLauncher.Modules.Settings.Application\DockLauncher.Modules.Settings.Application.csproj", "..\..\Modules\Panels\DockLauncher.Modules.Panels.Domain\DockLauncher.Modules.Panels.Domain.csproj", "..\..\Modules\Items\DockLauncher.Modules.Items.Domain\DockLauncher.Modules.Items.Domain.csproj") -PackageReferences ($testPackages + "System.IO.Abstractions.TestingHelpers")))
$projects.Add((New-ProjectDefinition -Name "DockLauncher.UiSmoke.Tests" -RelativePath "Tests/DockLauncher.UiSmoke.Tests/DockLauncher.UiSmoke.Tests.csproj" -Sdk "Microsoft.NET.Sdk" -TargetFramework "net9.0-windows" -ProjectReferences @("..\..\AppHost\DockLauncher.AppHost\DockLauncher.AppHost.csproj") -PackageReferences $testPackages))

foreach ($project in $projects) {
    $projectPath = Join-Path $srcRoot $project.RelativePath
    Write-Utf8File -Path $projectPath -Content (Get-CsProjContent -Project $project)
}

$appHostProjectPath = Join-Path $srcRoot "AppHost/DockLauncher.AppHost/DockLauncher.AppHost.csproj"
$appHostProject = Get-Content -Raw $appHostProjectPath
$appHostProject = $appHostProject -replace '</ItemGroup>\s+<ItemGroup>\s+<ProjectReference', "</ItemGroup>`n  <ItemGroup>`n    <None Update=`"appsettings.json`">`n      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`n    </None>`n    <None Update=`"appsettings.Development.json`">`n      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`n    </None>`n  </ItemGroup>`n  <ItemGroup>`n    <ProjectReference"
Write-Utf8File -Path $appHostProjectPath -Content $appHostProject

$packageProps = @'
<Project>
  <ItemGroup>
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageVersion Include="coverlet.collector" Version="6.0.2" />
    <PackageVersion Include="FluentAssertions" Version="6.12.1" />
    <PackageVersion Include="gong-wpf-dragdrop" Version="4.0.0" />
    <PackageVersion Include="H.NotifyIcon.Wpf" Version="2.3.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="9.0.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="NSubstitute" Version="5.3.0" />
    <PackageVersion Include="Serilog.Extensions.Logging" Version="9.0.0" />
    <PackageVersion Include="Serilog.Sinks.File" Version="6.0.0" />
    <PackageVersion Include="System.IO.Abstractions" Version="21.1.1" />
    <PackageVersion Include="System.IO.Abstractions.TestingHelpers" Version="21.1.1" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
</Project>
'@
Write-Utf8File -Path (Join-Path $srcRoot "Directory.Packages.props") -Content $packageProps

$buildProps = @'
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <LangVersion>preview</LangVersion>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>1591</NoWarn>
  </PropertyGroup>
  <ItemGroup Condition="$([System.String]::Copy('$(MSBuildProjectName)').Contains('.Tests'))">
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>
</Project>
'@
Write-Utf8File -Path (Join-Path $srcRoot "Directory.Build.props") -Content $buildProps

$globalJson = @'
{
  "sdk": {
    "version": "9.0.100",
    "rollForward": "latestFeature"
  }
}
'@
Write-Utf8File -Path (Join-Path $root "global.json") -Content $globalJson

$readme = @'
# DockLauncher

Windows-first dock and workspace launcher built as a modular monolith on .NET 9, WPF, MVVM, Generic Host, and JSON-backed local persistence.

## Status

- Milestone 1: solution skeleton with host bootstrapping
- MVP sample implementation: Panels, Items, Settings
- Test skeleton: domain, application, architecture, integration, UI smoke

## Current limitations

- The local machine currently does not expose a usable .NET SDK in `dotnet`; the repository is prepared for build and test, but execution requires installing or exposing a .NET 9 SDK.

## Planned workflow

1. Install or expose `.NET SDK 9`.
2. Run `dotnet restore src/DockLauncher.sln`.
3. Run `dotnet test src/DockLauncher.sln`.
4. Run `dotnet run --project src/AppHost/DockLauncher.AppHost`.
'@
Write-Utf8File -Path (Join-Path $root "README.md") -Content $readme

$architectureDoc = @'
# DockLauncher Architecture

## Solution shape

- `AppHost` composes configuration, DI, logging, and WPF startup.
- `BuildingBlocks` contains reusable abstractions.
- `Modules` isolate domain logic by feature.
- `Integrations` holds Windows-specific adapters.
- `Tests` mirrors the main architecture for verification.

## Initial MVP implementation

- `Panels`: panel aggregate, positioning, appearance settings, sample query.
- `Items`: launcher item aggregate and target validation.
- `Settings`: app settings, workspace model, JSON persistence, sample workspace seed.

## Persistence

- System settings: `appsettings.json`
- User workspace: `%AppData%/DockLauncher/workspace.json`

## Composition

`DockLauncher.AppHost` owns the Generic Host and uses each module's registration extension.
'@
Write-Utf8File -Path (Join-Path $srcRoot "docs/ARCHITECTURE.md") -Content $architectureDoc

$utf8Marker = @'
using System.Reflection;

[assembly: AssemblyMetadata("DockLauncher", "Generated")]
'@
Write-Utf8File -Path (Join-Path $srcRoot "eng/AssemblyInfo.cs") -Content $utf8Marker

$domainBase = @'
namespace DockLauncher.BuildingBlocks.Domain.Abstractions;

public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    protected Entity(TId id)
    {
        Id = id;
    }

    public TId Id { get; }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj)
    {
        return obj is Entity<TId> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GetType(), Id);
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Domain/Abstractions/Entity.cs") -Content $domainBase

$aggregateRoot = @'
namespace DockLauncher.BuildingBlocks.Domain.Abstractions;

public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    protected AggregateRoot(TId id)
        : base(id)
    {
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Domain/Abstractions/AggregateRoot.cs") -Content $aggregateRoot

$errorContent = @'
namespace DockLauncher.BuildingBlocks.Domain.Results;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Domain/Results/Error.cs") -Content $errorContent

$resultContent = @'
namespace DockLauncher.BuildingBlocks.Domain.Results;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);
}

public sealed class Result<TValue> : Result
{
    private Result(bool isSuccess, TValue? value, Error error)
        : base(isSuccess, error)
    {
        Value = value;
    }

    public TValue? Value { get; }

    public static Result<TValue> Success(TValue value) => new(true, value, Error.None);

    public static new Result<TValue> Failure(Error error) => new(false, default, error);
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Domain/Results/Result.cs") -Content $resultContent

$guardContent = @'
namespace DockLauncher.BuildingBlocks.Domain.Guards;

public static class Guard
{
    public static string AgainstNullOrWhiteSpace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }

        return value;
    }

    public static T AgainstNull<T>(T? value, string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        return value;
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Domain/Guards/Guard.cs") -Content $guardContent

$valueObjectContent = @'
namespace DockLauncher.BuildingBlocks.Domain.ValueObjects;

public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetAtomicValues();

    public override bool Equals(object? obj)
    {
        if (obj is not ValueObject other || GetType() != obj.GetType())
        {
            return false;
        }

        return GetAtomicValues().SequenceEqual(other.GetAtomicValues());
    }

    public override int GetHashCode()
    {
        return GetAtomicValues()
            .Aggregate(17, (current, value) => current * 31 + (value?.GetHashCode() ?? 0));
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Domain/ValueObjects/ValueObject.cs") -Content $valueObjectContent

$commandContent = @'
namespace DockLauncher.BuildingBlocks.Application.Abstractions;

public interface ICommand<out TResult>
{
}

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQuery<out TResult>
{
}

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Application/Abstractions/Messaging.cs") -Content $commandContent

$clockContent = @'
namespace DockLauncher.BuildingBlocks.Application.Contracts;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Application/Contracts/IClock.cs") -Content $clockContent

$jsonSerializerContent = @'
namespace DockLauncher.BuildingBlocks.Application.Contracts;

public interface IJsonSerializer
{
    string Serialize<T>(T value);
    T? Deserialize<T>(string json);
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Application/Contracts/IJsonSerializer.cs") -Content $jsonSerializerContent

$systemClockContent = @'
using DockLauncher.BuildingBlocks.Application.Contracts;

namespace DockLauncher.BuildingBlocks.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Infrastructure/Time/SystemClock.cs") -Content $systemClockContent

$serializerImpl = @'
using System.Text.Json;
using DockLauncher.BuildingBlocks.Application.Contracts;

namespace DockLauncher.BuildingBlocks.Infrastructure.Serialization;

public sealed class SystemTextJsonSerializer : IJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }

    public T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, Options);
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Infrastructure/Serialization/SystemTextJsonSerializer.cs") -Content $serializerImpl

$pathsContent = @'
namespace DockLauncher.BuildingBlocks.Infrastructure.FileSystem;

public sealed class AppDataPaths
{
    public AppDataPaths()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DockLauncher");

        Root = root;
        WorkspaceFilePath = Path.Combine(root, "workspace.json");
        LogDirectory = Path.Combine(root, "logs");
        IconsDirectory = Path.Combine(root, "icons");
    }

    public string Root { get; }
    public string WorkspaceFilePath { get; }
    public string LogDirectory { get; }
    public string IconsDirectory { get; }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Infrastructure/FileSystem/AppDataPaths.cs") -Content $pathsContent

$infraRegistration = @'
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
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Infrastructure/DependencyInjection.cs") -Content $infraRegistration

$presentationBase = @'
using CommunityToolkit.Mvvm.ComponentModel;

namespace DockLauncher.BuildingBlocks.Presentation.Wpf;

public abstract partial class ViewModelBase : ObservableObject
{
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Presentation.Wpf/ViewModelBase.cs") -Content $presentationBase

$themeContent = @'
namespace DockLauncher.BuildingBlocks.Presentation.Wpf.Theming;

public static class ThemeKeys
{
    public const string PanelBackgroundBrush = "PanelBackgroundBrush";
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "BuildingBlocks/DockLauncher.BuildingBlocks.Presentation.Wpf/Theming/ThemeKeys.cs") -Content $themeContent

$panelDomain = @'
using DockLauncher.BuildingBlocks.Domain.Abstractions;
using DockLauncher.BuildingBlocks.Domain.Guards;

namespace DockLauncher.Modules.Panels.Domain;

public sealed class Panel : AggregateRoot<Guid>
{
    private readonly List<Guid> _itemIds = [];

    public Panel(
        Guid id,
        string name,
        PanelPosition position,
        PanelLayoutMode layoutMode,
        PanelAppearance appearance)
        : base(id)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Position = position;
        LayoutMode = layoutMode;
        Appearance = appearance;
    }

    public string Name { get; private set; }

    public PanelPosition Position { get; private set; }

    public PanelLayoutMode LayoutMode { get; private set; }

    public PanelAppearance Appearance { get; private set; }

    public IReadOnlyCollection<Guid> ItemIds => _itemIds;

    public void Rename(string name)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
    }

    public void AddItem(Guid itemId)
    {
        if (!_itemIds.Contains(itemId))
        {
            _itemIds.Add(itemId);
        }
    }
}

public enum PanelPosition
{
    Top,
    Bottom,
    Left,
    Right,
    Floating
}

public enum PanelLayoutMode
{
    IconOnly,
    IconWithLabel,
    CompactList,
    Tiles,
    Grid
}

public sealed record PanelAppearance(double Opacity, int IconSize, bool AlwaysOnTop, bool LabelsVisible, bool AutoHide);
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/Panels/DockLauncher.Modules.Panels.Domain/Panel.cs") -Content $panelDomain

$panelsApp = @'
using DockLauncher.BuildingBlocks.Application.Abstractions;
using DockLauncher.Modules.Panels.Domain;

namespace DockLauncher.Modules.Panels.Application;

public interface IPanelRepository
{
    Task<IReadOnlyList<Panel>> GetAllAsync(CancellationToken cancellationToken);
}

public sealed record GetPanelsQuery : IQuery<IReadOnlyList<Panel>>;

public sealed class GetPanelsQueryHandler : IQueryHandler<GetPanelsQuery, IReadOnlyList<Panel>>
{
    private readonly IPanelRepository _repository;

    public GetPanelsQueryHandler(IPanelRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<Panel>> HandleAsync(GetPanelsQuery query, CancellationToken cancellationToken)
    {
        return _repository.GetAllAsync(cancellationToken);
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/Panels/DockLauncher.Modules.Panels.Application/PanelsQueries.cs") -Content $panelsApp

$panelsInfra = @'
using DockLauncher.Modules.Panels.Application;
using DockLauncher.Modules.Panels.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.Modules.Panels.Infrastructure;

public static class PanelsModule
{
    public static IServiceCollection AddPanelsModule(this IServiceCollection services)
    {
        services.AddSingleton<IPanelRepository, SeededPanelRepository>();
        services.AddTransient<GetPanelsQueryHandler>();
        return services;
    }
}

internal sealed class SeededPanelRepository : IPanelRepository
{
    private static readonly IReadOnlyList<Panel> Panels =
    [
        new Panel(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Work",
            PanelPosition.Bottom,
            PanelLayoutMode.IconWithLabel,
            new PanelAppearance(0.92, 40, true, true, false)),
        new Panel(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Dev",
            PanelPosition.Left,
            PanelLayoutMode.Grid,
            new PanelAppearance(0.88, 36, true, false, true))
    ];

    public Task<IReadOnlyList<Panel>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Panels);
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/Panels/DockLauncher.Modules.Panels.Infrastructure/PanelsModule.cs") -Content $panelsInfra

$panelsPresentation = @'
using CommunityToolkit.Mvvm.ComponentModel;
using DockLauncher.BuildingBlocks.Presentation.Wpf;
using DockLauncher.Modules.Panels.Application;

namespace DockLauncher.Modules.Panels.Presentation.Wpf;

public sealed partial class PanelsOverviewViewModel : ViewModelBase
{
    private readonly GetPanelsQueryHandler _handler;

    public PanelsOverviewViewModel(GetPanelsQueryHandler handler)
    {
        _handler = handler;
    }

    [ObservableProperty]
    private IReadOnlyList<PanelTileViewModel> panels = [];

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await _handler.HandleAsync(new GetPanelsQuery(), cancellationToken);
        Panels = result
            .Select(panel => new PanelTileViewModel(panel.Name, panel.Position.ToString(), panel.LayoutMode.ToString()))
            .ToArray();
    }
}

public sealed record PanelTileViewModel(string Name, string Position, string LayoutMode);
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/Panels/DockLauncher.Modules.Panels.Presentation.Wpf/PanelsOverviewViewModel.cs") -Content $panelsPresentation

$itemsDomain = @'
using DockLauncher.BuildingBlocks.Domain.Abstractions;
using DockLauncher.BuildingBlocks.Domain.Guards;

namespace DockLauncher.Modules.Items.Domain;

public sealed class LauncherItem : AggregateRoot<Guid>
{
    public LauncherItem(Guid id, string displayName, LauncherItemType type, string target, string? arguments = null, bool runAsAdministrator = false)
        : base(id)
    {
        DisplayName = Guard.AgainstNullOrWhiteSpace(displayName, nameof(displayName));
        Type = type;
        Target = Guard.AgainstNullOrWhiteSpace(target, nameof(target));
        Arguments = arguments;
        RunAsAdministrator = runAsAdministrator;
    }

    public string DisplayName { get; }
    public LauncherItemType Type { get; }
    public string Target { get; }
    public string? Arguments { get; }
    public bool RunAsAdministrator { get; }
}

public enum LauncherItemType
{
    Application,
    Shortcut,
    Folder,
    File,
    Url,
    Command,
    Action
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/Items/DockLauncher.Modules.Items.Domain/LauncherItem.cs") -Content $itemsDomain

$itemsApp = @'
using DockLauncher.BuildingBlocks.Application.Abstractions;
using DockLauncher.BuildingBlocks.Domain.Results;
using DockLauncher.Modules.Items.Domain;

namespace DockLauncher.Modules.Items.Application;

public interface ILauncherItemService
{
    Task<Result> LaunchAsync(LauncherItem item, CancellationToken cancellationToken);
}

public sealed record LaunchItemCommand(LauncherItem Item) : ICommand<Result>;

public sealed class LaunchItemCommandHandler : ICommandHandler<LaunchItemCommand, Result>
{
    private readonly ILauncherItemService _service;

    public LaunchItemCommandHandler(ILauncherItemService service)
    {
        _service = service;
    }

    public Task<Result> HandleAsync(LaunchItemCommand command, CancellationToken cancellationToken)
    {
        return _service.LaunchAsync(command.Item, cancellationToken);
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/Items/DockLauncher.Modules.Items.Application/LaunchItemCommand.cs") -Content $itemsApp

$integrationsContent = @'
using System.Diagnostics;
using DockLauncher.BuildingBlocks.Domain.Results;
using DockLauncher.Modules.Items.Application;
using DockLauncher.Modules.Items.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.Integrations.Windows;

public static class DependencyInjection
{
    public static IServiceCollection AddWindowsIntegrations(this IServiceCollection services)
    {
        services.AddSingleton<ILauncherItemService, WindowsLauncherItemService>();
        return services;
    }
}

internal sealed class WindowsLauncherItemService : ILauncherItemService
{
    public Task<Result> LaunchAsync(LauncherItem item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.Target))
        {
            return Task.FromResult(Result.Failure(new Error("items.target.empty", "Launcher item target is missing.")));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = item.Target,
            Arguments = item.Arguments ?? string.Empty,
            UseShellExecute = true,
            Verb = item.RunAsAdministrator ? "runas" : string.Empty
        };

        _ = startInfo;
        return Task.FromResult(Result.Success());
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Integrations/DockLauncher.Integrations.Windows/WindowsLauncherItemService.cs") -Content $integrationsContent

$itemsInfra = @'
using DockLauncher.Modules.Items.Application;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.Modules.Items.Infrastructure;

public static class ItemsModule
{
    public static IServiceCollection AddItemsModule(this IServiceCollection services)
    {
        services.AddTransient<LaunchItemCommandHandler>();
        return services;
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/Items/DockLauncher.Modules.Items.Infrastructure/ItemsModule.cs") -Content $itemsInfra

$itemsPresentation = @'
using DockLauncher.BuildingBlocks.Presentation.Wpf;

namespace DockLauncher.Modules.Items.Presentation.Wpf;

public sealed record LauncherItemCardViewModel(string DisplayName, string Type, string Target);

public sealed partial class ItemsViewModel : ViewModelBase
{
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/Items/DockLauncher.Modules.Items.Presentation.Wpf/ItemsViewModel.cs") -Content $itemsPresentation

$settingsDomain = @'
using DockLauncher.Modules.Items.Domain;
using DockLauncher.Modules.Panels.Domain;

namespace DockLauncher.Modules.Settings.Domain;

public sealed record AppSettings(
    string Language,
    string Theme,
    bool StartWithWindows,
    string GlobalHotkey);

public sealed record Workspace(
    int SchemaVersion,
    AppSettings Settings,
    IReadOnlyList<Panel> Panels,
    IReadOnlyList<LauncherItem> Items);
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/Settings/DockLauncher.Modules.Settings.Domain/Workspace.cs") -Content $settingsDomain

$settingsApp = @'
using DockLauncher.BuildingBlocks.Application.Abstractions;
using DockLauncher.Modules.Settings.Domain;

namespace DockLauncher.Modules.Settings.Application;

public interface IWorkspaceStore
{
    Task<Workspace> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(Workspace workspace, CancellationToken cancellationToken);
}

public sealed record LoadWorkspaceQuery : IQuery<Workspace>;

public sealed class LoadWorkspaceQueryHandler : IQueryHandler<LoadWorkspaceQuery, Workspace>
{
    private readonly IWorkspaceStore _workspaceStore;

    public LoadWorkspaceQueryHandler(IWorkspaceStore workspaceStore)
    {
        _workspaceStore = workspaceStore;
    }

    public Task<Workspace> HandleAsync(LoadWorkspaceQuery query, CancellationToken cancellationToken)
    {
        return _workspaceStore.LoadAsync(cancellationToken);
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/Settings/DockLauncher.Modules.Settings.Application/WorkspaceQueries.cs") -Content $settingsApp

$settingsInfra = @'
using System.IO.Abstractions;
using DockLauncher.BuildingBlocks.Application.Contracts;
using DockLauncher.BuildingBlocks.Infrastructure.FileSystem;
using DockLauncher.Modules.Items.Domain;
using DockLauncher.Modules.Panels.Domain;
using DockLauncher.Modules.Settings.Application;
using DockLauncher.Modules.Settings.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.Modules.Settings.Infrastructure;

public static class SettingsModule
{
    public static IServiceCollection AddSettingsModule(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IWorkspaceStore, JsonWorkspaceStore>();
        services.AddTransient<LoadWorkspaceQueryHandler>();
        return services;
    }
}

public sealed class JsonWorkspaceStore : IWorkspaceStore
{
    private readonly IFileSystem _fileSystem;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly AppDataPaths _paths;

    public JsonWorkspaceStore(IFileSystem fileSystem, IJsonSerializer jsonSerializer, AppDataPaths paths)
    {
        _fileSystem = fileSystem;
        _jsonSerializer = jsonSerializer;
        _paths = paths;
    }

    public async Task<Workspace> LoadAsync(CancellationToken cancellationToken)
    {
        if (!_fileSystem.File.Exists(_paths.WorkspaceFilePath))
        {
            return CreateDefaultWorkspace();
        }

        var json = await _fileSystem.File.ReadAllTextAsync(_paths.WorkspaceFilePath, cancellationToken);
        return _jsonSerializer.Deserialize<Workspace>(json) ?? CreateDefaultWorkspace();
    }

    public async Task SaveAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        _fileSystem.Directory.CreateDirectory(_paths.Root);
        var json = _jsonSerializer.Serialize(workspace);
        await _fileSystem.File.WriteAllTextAsync(_paths.WorkspaceFilePath, json, cancellationToken);
    }

    private static Workspace CreateDefaultWorkspace()
    {
        var panel = new Panel(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Starter",
            PanelPosition.Bottom,
            PanelLayoutMode.IconWithLabel,
            new PanelAppearance(0.9, 40, true, true, false));

        var items = new[]
        {
            new LauncherItem(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                "Explorer",
                LauncherItemType.Application,
                "explorer.exe")
        };

        panel.AddItem(items[0].Id);

        return new Workspace(
            1,
            new AppSettings("en", "system", false, "Alt+Space"),
            new[] { panel },
            items);
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/Settings/DockLauncher.Modules.Settings.Infrastructure/JsonWorkspaceStore.cs") -Content $settingsInfra

$settingsPresentation = @'
using CommunityToolkit.Mvvm.ComponentModel;
using DockLauncher.BuildingBlocks.Presentation.Wpf;
using DockLauncher.Modules.Settings.Application;

namespace DockLauncher.Modules.Settings.Presentation.Wpf;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly LoadWorkspaceQueryHandler _handler;

    public SettingsViewModel(LoadWorkspaceQueryHandler handler)
    {
        _handler = handler;
    }

    [ObservableProperty]
    private string summary = "Workspace not loaded.";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var workspace = await _handler.HandleAsync(new LoadWorkspaceQuery(), cancellationToken);
        Summary = $"{workspace.Settings.Language} | {workspace.Settings.Theme} | Panels: {workspace.Panels.Count} | Items: {workspace.Items.Count}";
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/Settings/DockLauncher.Modules.Settings.Presentation.Wpf/SettingsViewModel.cs") -Content $settingsPresentation

$launchProfilesDomain = @'
namespace DockLauncher.Modules.LaunchProfiles.Domain;

public sealed record LaunchProfile(Guid Id, string Name, IReadOnlyList<LaunchStep> Steps);

public sealed record LaunchStep(Guid ItemId, int DelayMs, bool RunAsAdministrator);
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/LaunchProfiles/DockLauncher.Modules.LaunchProfiles.Domain/LaunchProfile.cs") -Content $launchProfilesDomain

$launchProfilesApp = @'
using DockLauncher.BuildingBlocks.Application.Abstractions;
using DockLauncher.Modules.LaunchProfiles.Domain;

namespace DockLauncher.Modules.LaunchProfiles.Application;

public interface ILaunchProfileRunner
{
    Task RunAsync(LaunchProfile profile, CancellationToken cancellationToken);
}

public sealed record RunLaunchProfileCommand(LaunchProfile Profile) : ICommand<bool>;

public sealed class RunLaunchProfileCommandHandler : ICommandHandler<RunLaunchProfileCommand, bool>
{
    private readonly ILaunchProfileRunner _runner;

    public RunLaunchProfileCommandHandler(ILaunchProfileRunner runner)
    {
        _runner = runner;
    }

    public async Task<bool> HandleAsync(RunLaunchProfileCommand command, CancellationToken cancellationToken)
    {
        await _runner.RunAsync(command.Profile, cancellationToken);
        return true;
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/LaunchProfiles/DockLauncher.Modules.LaunchProfiles.Application/RunLaunchProfileCommand.cs") -Content $launchProfilesApp

$launchProfilesInfra = @'
using DockLauncher.Modules.LaunchProfiles.Application;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.Modules.LaunchProfiles.Infrastructure;

public static class LaunchProfilesModule
{
    public static IServiceCollection AddLaunchProfilesModule(this IServiceCollection services)
    {
        services.AddSingleton<ILaunchProfileRunner, DelayedLaunchProfileRunner>();
        services.AddTransient<RunLaunchProfileCommandHandler>();
        return services;
    }
}

internal sealed class DelayedLaunchProfileRunner : ILaunchProfileRunner
{
    public async Task RunAsync(Domain.LaunchProfile profile, CancellationToken cancellationToken)
    {
        foreach (var step in profile.Steps)
        {
            if (step.DelayMs > 0)
            {
                await Task.Delay(step.DelayMs, cancellationToken);
            }
        }
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Modules/LaunchProfiles/DockLauncher.Modules.LaunchProfiles.Infrastructure/LaunchProfilesModule.cs") -Content $launchProfilesInfra

foreach ($module in @("Groups", "FolderFlyouts", "Icons", "Tray", "Hotkeys", "ShellIntegration")) {
    $domainNamespace = "DockLauncher.Modules.$module.Domain"
    $appNamespace = "DockLauncher.Modules.$module.Application"
    $infraNamespace = "DockLauncher.Modules.$module.Infrastructure"
    $presentationNamespace = "DockLauncher.Modules.$module.Presentation.Wpf"
    $moduleFolder = Join-Path $srcRoot "Modules/$module"

    Write-Utf8File -Path (Join-Path $moduleFolder "DockLauncher.Modules.$module.Domain/AssemblyMarker.cs") -Content "namespace $domainNamespace; public static class AssemblyMarker { }"
    Write-Utf8File -Path (Join-Path $moduleFolder "DockLauncher.Modules.$module.Application/DependencyRegistration.cs") -Content "using Microsoft.Extensions.DependencyInjection; namespace $appNamespace; public static class DependencyRegistration { public static IServiceCollection Add${module}Application(this IServiceCollection services) => services; }"
    Write-Utf8File -Path (Join-Path $moduleFolder "DockLauncher.Modules.$module.Infrastructure/ModuleRegistration.cs") -Content "using Microsoft.Extensions.DependencyInjection; namespace $infraNamespace; public static class ModuleRegistration { public static IServiceCollection Add${module}Module(this IServiceCollection services) => services; }"
    Write-Utf8File -Path (Join-Path $moduleFolder "DockLauncher.Modules.$module.Presentation.Wpf/AssemblyMarker.cs") -Content "namespace $presentationNamespace; public static class AssemblyMarker { }"
}

$appOptions = @'
namespace DockLauncher.AppHost.Configuration;

public sealed class ShellOptions
{
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "system";
    public bool StartWithWindows { get; set; }
    public string GlobalHotkey { get; set; } = "Alt+Space";
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "AppHost/DockLauncher.AppHost/Configuration/ShellOptions.cs") -Content $appOptions

$hostBuilder = @'
using System.IO;
using DockLauncher.AppHost.Configuration;
using DockLauncher.BuildingBlocks.Infrastructure;
using DockLauncher.Integrations.Windows;
using DockLauncher.Modules.FolderFlyouts.Infrastructure;
using DockLauncher.Modules.Groups.Infrastructure;
using DockLauncher.Modules.Hotkeys.Infrastructure;
using DockLauncher.Modules.Icons.Infrastructure;
using DockLauncher.Modules.Items.Infrastructure;
using DockLauncher.Modules.LaunchProfiles.Infrastructure;
using DockLauncher.Modules.Panels.Infrastructure;
using DockLauncher.Modules.Settings.Infrastructure;
using DockLauncher.Modules.ShellIntegration.Infrastructure;
using DockLauncher.Modules.Tray.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DockLauncher.AppHost.Hosting;

public static class HostBuilderFactory
{
    public static IHost Build()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);

        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DockLauncher",
            "logs",
            "docklauncher-.log");

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);

        builder.Services.Configure<ShellOptions>(builder.Configuration.GetSection("Shell"));
        builder.Services.AddBuildingBlocksInfrastructure();
        builder.Services.AddWindowsIntegrations();
        builder.Services.AddPanelsModule();
        builder.Services.AddItemsModule();
        builder.Services.AddSettingsModule();
        builder.Services.AddGroupsModule();
        builder.Services.AddLaunchProfilesModule();
        builder.Services.AddFolderFlyoutsModule();
        builder.Services.AddIconsModule();
        builder.Services.AddTrayModule();
        builder.Services.AddHotkeysModule();
        builder.Services.AddShellIntegrationModule();

        Composition.ServiceCollectionExtensions.AddApplicationShell(builder.Services);

        return builder.Build();
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "AppHost/DockLauncher.AppHost/Hosting/HostBuilderFactory.cs") -Content $hostBuilder

$composition = @'
using DockLauncher.Modules.Panels.Presentation.Wpf;
using DockLauncher.Modules.Settings.Presentation.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.AppHost.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationShell(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddTransient<PanelsOverviewViewModel>();
        services.AddTransient<SettingsViewModel>();
        return services;
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "AppHost/DockLauncher.AppHost/Composition/ServiceCollectionExtensions.cs") -Content $composition

$program = @'
using System.Windows;

namespace DockLauncher.AppHost;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "AppHost/DockLauncher.AppHost/Program.cs") -Content $program

$appXaml = @'
<Application x:Class="DockLauncher.AppHost.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <SolidColorBrush x:Key="PanelBackgroundBrush" Color="#FF1F2937" />
    </Application.Resources>
</Application>
'@
Write-Utf8File -Path (Join-Path $srcRoot "AppHost/DockLauncher.AppHost/App.xaml") -Content $appXaml

$appXamlCs = @'
using System.Windows;
using DockLauncher.AppHost.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DockLauncher.AppHost;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = HostBuilderFactory.Build();
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "AppHost/DockLauncher.AppHost/App.xaml.cs") -Content $appXamlCs

$mainWindowXaml = @'
<Window x:Class="DockLauncher.AppHost.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="DockLauncher"
        Width="1080"
        Height="640"
        WindowStartupLocation="CenterScreen"
        Background="#FF111827"
        Foreground="White">
    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <StackPanel>
            <TextBlock FontSize="30" FontWeight="SemiBold" Text="DockLauncher" />
            <TextBlock Margin="0,8,0,0"
                       Opacity="0.8"
                       Text="Windows-first dock skeleton: panels, items, settings, and host composition." />
        </StackPanel>

        <Grid Grid.Row="1" Margin="0,24,0,24">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="2*" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>

            <Border Margin="0,0,16,0" Padding="16" Background="#FF1F2937" CornerRadius="16">
                <ItemsControl ItemsSource="{Binding Panels.Panels}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Margin="0,0,0,12" Padding="12" Background="#FF374151" CornerRadius="12">
                                <StackPanel>
                                    <TextBlock FontSize="18" FontWeight="SemiBold" Text="{Binding Name}" />
                                    <TextBlock Margin="0,4,0,0" Opacity="0.7" Text="{Binding Position}" />
                                    <TextBlock Margin="0,2,0,0" Opacity="0.7" Text="{Binding LayoutMode}" />
                                </StackPanel>
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </Border>

            <Border Grid.Column="1" Padding="16" Background="#FF1F2937" CornerRadius="16">
                <StackPanel>
                    <TextBlock FontSize="20" FontWeight="SemiBold" Text="Settings Snapshot" />
                    <TextBlock Margin="0,16,0,0" TextWrapping="Wrap" Text="{Binding Settings.Summary}" />
                </StackPanel>
            </Border>
        </Grid>

        <Border Grid.Row="2" Padding="12" Background="#FF1F2937" CornerRadius="12">
            <TextBlock Opacity="0.75"
                       Text="Next milestone: drag-and-drop, tray behavior, folder flyouts, and persisted workspace editing." />
        </Border>
    </Grid>
</Window>
'@
Write-Utf8File -Path (Join-Path $srcRoot "AppHost/DockLauncher.AppHost/MainWindow.xaml") -Content $mainWindowXaml

$mainWindowCs = @'
using System.Windows;
using DockLauncher.Modules.Panels.Presentation.Wpf;
using DockLauncher.Modules.Settings.Presentation.Wpf;

namespace DockLauncher.AppHost;

public partial class MainWindow : Window
{
    public MainWindow(PanelsOverviewViewModel panelsViewModel, SettingsViewModel settingsViewModel)
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(panelsViewModel, settingsViewModel);
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}

public sealed class MainWindowViewModel
{
    public MainWindowViewModel(PanelsOverviewViewModel panels, SettingsViewModel settings)
    {
        Panels = panels;
        Settings = settings;
    }

    public PanelsOverviewViewModel Panels { get; }

    public SettingsViewModel Settings { get; }

    public async Task InitializeAsync()
    {
        await Panels.LoadAsync();
        await Settings.LoadAsync();
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "AppHost/DockLauncher.AppHost/MainWindow.xaml.cs") -Content $mainWindowCs

$appSettingsJson = @'
{
  "Shell": {
    "language": "en",
    "theme": "system",
    "startWithWindows": false,
    "globalHotkey": "Alt+Space"
  }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "AppHost/DockLauncher.AppHost/appsettings.json") -Content $appSettingsJson

$buildingBlocksTest = @'
using DockLauncher.BuildingBlocks.Domain.Results;
using FluentAssertions;

namespace DockLauncher.BuildingBlocks.Domain.Tests;

public class ResultTests
{
    [Fact]
    public void Success_ShouldExposeSuccessfulState()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Tests/DockLauncher.BuildingBlocks.Domain.Tests/ResultTests.cs") -Content $buildingBlocksTest

$panelsDomainTest = @'
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
'@
Write-Utf8File -Path (Join-Path $srcRoot "Tests/DockLauncher.Modules.Panels.Domain.Tests/PanelTests.cs") -Content $panelsDomainTest

$panelsAppTest = @'
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
'@
Write-Utf8File -Path (Join-Path $srcRoot "Tests/DockLauncher.Modules.Panels.Application.Tests/GetPanelsQueryHandlerTests.cs") -Content $panelsAppTest

$itemsDomainTest = @'
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
'@
Write-Utf8File -Path (Join-Path $srcRoot "Tests/DockLauncher.Modules.Items.Domain.Tests/LauncherItemTests.cs") -Content $itemsDomainTest

$launchProfileTest = @'
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
'@
Write-Utf8File -Path (Join-Path $srcRoot "Tests/DockLauncher.Modules.LaunchProfiles.Application.Tests/RunLaunchProfileCommandHandlerTests.cs") -Content $launchProfileTest

$architectureTests = @'
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
'@
Write-Utf8File -Path (Join-Path $srcRoot "Tests/DockLauncher.Architecture.Tests/DependencyRulesTests.cs") -Content $architectureTests

$integrationTests = @'
using DockLauncher.BuildingBlocks.Infrastructure.FileSystem;
using DockLauncher.BuildingBlocks.Infrastructure.Serialization;
using DockLauncher.Modules.Settings.Infrastructure;
using FluentAssertions;
using System.IO.Abstractions.TestingHelpers;

namespace DockLauncher.Integration.Tests;

public class JsonWorkspaceStoreTests
{
    [Fact]
    public async Task LoadAsync_ShouldReturnDefaultWorkspace_WhenFileDoesNotExist()
    {
        var fileSystem = new MockFileSystem();
        var store = new JsonWorkspaceStore(fileSystem, new SystemTextJsonSerializer(), new AppDataPaths());

        var workspace = await store.LoadAsync(CancellationToken.None);

        workspace.Panels.Should().NotBeEmpty();
        workspace.Items.Should().NotBeEmpty();
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Tests/DockLauncher.Integration.Tests/JsonWorkspaceStoreTests.cs") -Content $integrationTests

$uiSmokeTests = @'
using DockLauncher.AppHost;
using FluentAssertions;

namespace DockLauncher.UiSmoke.Tests;

public class AppShellTests
{
    [Fact]
    public void MainWindowViewModel_ShouldExposeChildViewModels()
    {
        var panels = new DockLauncher.Modules.Panels.Presentation.Wpf.PanelsOverviewViewModel(new DockLauncher.Modules.Panels.Application.GetPanelsQueryHandler(new StubPanelRepository()));
        var settings = new DockLauncher.Modules.Settings.Presentation.Wpf.SettingsViewModel(new DockLauncher.Modules.Settings.Application.LoadWorkspaceQueryHandler(new StubWorkspaceStore()));
        var viewModel = new MainWindowViewModel(panels, settings);

        viewModel.Panels.Should().BeSameAs(panels);
        viewModel.Settings.Should().BeSameAs(settings);
    }

    private sealed class StubPanelRepository : DockLauncher.Modules.Panels.Application.IPanelRepository
    {
        public Task<IReadOnlyList<DockLauncher.Modules.Panels.Domain.Panel>> GetAllAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<DockLauncher.Modules.Panels.Domain.Panel> panels = [];
            return Task.FromResult(panels);
        }
    }

    private sealed class StubWorkspaceStore : DockLauncher.Modules.Settings.Application.IWorkspaceStore
    {
        public Task<DockLauncher.Modules.Settings.Domain.Workspace> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DockLauncher.Modules.Settings.Domain.Workspace(
                1,
                new DockLauncher.Modules.Settings.Domain.AppSettings("en", "system", false, "Alt+Space"),
                [],
                []));
        }

        public Task SaveAsync(DockLauncher.Modules.Settings.Domain.Workspace workspace, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
'@
Write-Utf8File -Path (Join-Path $srcRoot "Tests/DockLauncher.UiSmoke.Tests/AppShellTests.cs") -Content $uiSmokeTests

$slnLines = [System.Collections.Generic.List[string]]::new()
$slnLines.Add("Microsoft Visual Studio Solution File, Format Version 12.00")
$slnLines.Add("# Visual Studio Version 17")
$slnLines.Add("VisualStudioVersion = 17.0.31903.59")
$slnLines.Add("MinimumVisualStudioVersion = 10.0.40219.1")

foreach ($folder in $solutionFolders) {
    $slnLines.Add(('Project("{{2150E333-8FDC-42A3-9474-1A3956D46DE8}}") = "{0}", "{0}", "{{{1}}}"' -f $folder.Name, $folder.Guid))
    $slnLines.Add("EndProject")
}

foreach ($project in $projects) {
    $path = $project.RelativePath -replace "/", "\"
    $slnLines.Add(('Project("{0}") = "{1}", "{2}", "{{{3}}}"' -f $projectTypeGuid, $project.Name, $path, $project.Guid))
    $slnLines.Add("EndProject")
}

$slnLines.Add("Global")
$slnLines.Add("`tGlobalSection(SolutionConfigurationPlatforms) = preSolution")
$slnLines.Add("`t`tDebug|Any CPU = Debug|Any CPU")
$slnLines.Add("`t`tRelease|Any CPU = Release|Any CPU")
$slnLines.Add("`tEndGlobalSection")
$slnLines.Add("`tGlobalSection(ProjectConfigurationPlatforms) = postSolution")
foreach ($project in $projects) {
    $slnLines.Add(("`t`t{{{0}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU" -f $project.Guid))
    $slnLines.Add(("`t`t{{{0}}}.Debug|Any CPU.Build.0 = Debug|Any CPU" -f $project.Guid))
    $slnLines.Add(("`t`t{{{0}}}.Release|Any CPU.ActiveCfg = Release|Any CPU" -f $project.Guid))
    $slnLines.Add(("`t`t{{{0}}}.Release|Any CPU.Build.0 = Release|Any CPU" -f $project.Guid))
}
$slnLines.Add("`tEndGlobalSection")
$slnLines.Add("`tGlobalSection(SolutionProperties) = preSolution")
$slnLines.Add("`t`tHideSolutionNode = FALSE")
$slnLines.Add("`tEndGlobalSection")
$slnLines.Add("`tGlobalSection(NestedProjects) = preSolution")

$folderMap = @{
    "AppHost" = ($solutionFolders | Where-Object Name -eq "AppHost").Guid
    "BuildingBlocks" = ($solutionFolders | Where-Object Name -eq "BuildingBlocks").Guid
    "Modules" = ($solutionFolders | Where-Object Name -eq "Modules").Guid
    "Integrations" = ($solutionFolders | Where-Object Name -eq "Integrations").Guid
    "Tests" = ($solutionFolders | Where-Object Name -eq "Tests").Guid
}

foreach ($project in $projects) {
    $top = ($project.RelativePath -split "/")[0]
    if ($folderMap.ContainsKey($top)) {
        $slnLines.Add(("`t`t{{{0}}} = {{{1}}}" -f $project.Guid, $folderMap[$top]))
    }
}

$slnLines.Add("`tEndGlobalSection")
$slnLines.Add("EndGlobal")

Write-Utf8File -Path $solutionPath -Content ($slnLines -join "`n")

Write-Host "DockLauncher skeleton generated at $srcRoot"
