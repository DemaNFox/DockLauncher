$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $root "build\DockLauncher"
$appOutput = Join-Path $packageRoot "app"
$launcherOutput = Join-Path $root "build\DockLauncherLauncher"

if (Test-Path $packageRoot) {
    Remove-Item -Recurse -Force $packageRoot
}

if (Test-Path $launcherOutput) {
    Remove-Item -Recurse -Force $launcherOutput
}

New-Item -ItemType Directory -Force -Path $appOutput | Out-Null

dotnet publish `
    (Join-Path $root "src\AppHost\DockLauncher.AppHost\DockLauncher.AppHost.csproj") `
    -c Release `
    -o $appOutput `
    /p:PublishSingleFile=false `
    /p:SelfContained=false `
    /p:DebugSymbols=false `
    /p:DebugType=none

dotnet publish `
    (Join-Path $root "src\AppHost\DockLauncher.Launcher\DockLauncher.Launcher.csproj") `
    -c Release `
    -o $launcherOutput

Copy-Item -Force (Join-Path $launcherOutput "DockLauncher.exe") (Join-Path $packageRoot "DockLauncher.exe")
Remove-Item -Recurse -Force $launcherOutput

Write-Host "DockLauncher package created at $packageRoot"
