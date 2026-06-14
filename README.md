# DockLauncher

DockLauncher is a Windows-first dock and workspace launcher built with .NET 9, WPF, MVVM, Generic Host, and JSON-backed local persistence.

## Project Layout

- `src/DockLauncher.sln` - main solution.
- `src/AppHost/DockLauncher.AppHost` - WPF application host.
- `src/Modules` - feature modules for panels, items, groups, settings, tray, hotkeys, and integrations.
- `src/BuildingBlocks` - shared domain, application, infrastructure, and WPF building blocks.
- `src/Tests` - unit, integration, architecture, E2E, and UI smoke tests.

## Requirements

- Windows.
- .NET SDK 9.

The repository includes `global.json` and rolls forward to the latest installed .NET 9 feature SDK.

## Build

```powershell
dotnet restore src\DockLauncher.sln
dotnet build src\DockLauncher.sln --no-restore
```

## Run

```powershell
dotnet run --project src\AppHost\DockLauncher.AppHost\DockLauncher.AppHost.csproj
```

## Test

```powershell
dotnet test src\DockLauncher.sln --no-build
```
