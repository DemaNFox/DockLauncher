[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$Installer,
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $root "build\DockLauncher"
$appOutput = Join-Path $packageRoot "app"
$launcherOutput = Join-Path $root "build\DockLauncherLauncher"
$installerScript = Join-Path $root "installer\DockLauncher.iss"
$installerOutput = Join-Path $root "artifacts\installer"

if (Test-Path $packageRoot) {
    Remove-Item -Recurse -Force $packageRoot
}

if (Test-Path $launcherOutput) {
    Remove-Item -Recurse -Force $launcherOutput
}

New-Item -ItemType Directory -Force -Path $appOutput | Out-Null

dotnet publish `
    (Join-Path $root "src\AppHost\DockLauncher.AppHost\DockLauncher.AppHost.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $appOutput `
    /p:PublishSingleFile=false `
    /p:DebugSymbols=false `
    /p:DebugType=none

dotnet publish `
    (Join-Path $root "src\AppHost\DockLauncher.Launcher\DockLauncher.Launcher.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $launcherOutput

Copy-Item -Force (Join-Path $launcherOutput "DockLauncher.exe") (Join-Path $packageRoot "DockLauncher.exe")
Remove-Item -Recurse -Force $launcherOutput

Write-Host "DockLauncher package created at $packageRoot"

if (-not $Installer) {
    Write-Host "Run with -Installer to create a Windows installer with Inno Setup."
    return
}

if (-not (Test-Path $installerScript)) {
    throw "Installer script was not found: $installerScript"
}

$iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
if ($null -eq $iscc) {
    $candidatePaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    $isccPath = $candidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($isccPath)) {
        throw "Inno Setup compiler was not found. Install Inno Setup 6, then run this script again with -Installer."
    }
}
else {
    $isccPath = $iscc.Source
}

New-Item -ItemType Directory -Force -Path $installerOutput | Out-Null
& $isccPath "/DAppVersion=$Version" $installerScript

Write-Host "DockLauncher installer created at $installerOutput"
