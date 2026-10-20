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

## Workspace editor

The DocSoft configurator has four sections: Appearance, Items, Groups, and Profiles. Select a panel in the left column, then edit its settings or shortcuts in the section on the right. Changes preview on the runtime panels and save to the workspace file automatically after a short pause. Closing the configurator or exiting DocSoft flushes pending edits; if saving fails, the window stays open and shows the error.

Import, Export, Reset, and Reload are available in the header. Reset replaces the current workspace. The DocSoft mark is shared by the window, executable, and tray icon; regenerate the icon assets with `python tools\Generate-DocSoftIcons.py` (Pillow required).

## Publish

Create a production package:

```powershell
powershell -ExecutionPolicy Bypass -File tools\Publish-DockLauncher.ps1
```

The package is written to `build\DockLauncher`.

Create a Windows installer:

```powershell
powershell -ExecutionPolicy Bypass -File tools\Publish-DockLauncher.ps1 -Installer
```

Installer builds require Inno Setup 6 (`ISCC.exe`). The generated installer is written to `artifacts\installer`.

## Test

```powershell
dotnet test src\DockLauncher.sln --no-build
```
