[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$Installer,
    [string]$Version = "1.0.7"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$installerScript = Join-Path $root "installer\DockLauncher.iss"
$installerOutput = Join-Path $root "artifacts\installer"

if ($Installer -and $Runtime -ne "win-x64") {
    throw "The installer currently supports win-x64 only."
}

dotnet publish `
    (Join-Path $root "src\AppHost\DockLauncher.AppHost\DockLauncher.AppHost.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:Version=$Version `
    /p:PublishSingleFile=false `
    /p:DebugSymbols=false `
    /p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw "DocSoft application publish failed." }

dotnet publish `
    (Join-Path $root "src\AppHost\DockLauncher.Launcher\DockLauncher.Launcher.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:Version=$Version `
    /p:DebugSymbols=false `
    /p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw "DocSoft launcher publish failed." }

Write-Host "DocSoft publish outputs created for $Runtime."

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
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
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
& $isccPath "/DAppVersion=$Version" "/DTargetRuntime=$Runtime" "/DBuildConfiguration=$Configuration" $installerScript
if ($LASTEXITCODE -ne 0) { throw "DocSoft installer compilation failed." }

Write-Host "DocSoft installer created at $installerOutput"
