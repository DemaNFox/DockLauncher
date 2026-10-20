# DocSoft

DocSoft is a Windows dock and workspace launcher built with .NET 9, WPF, MVVM, Generic Host, and JSON-backed local persistence. The repository and internal project names remain DockLauncher.

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

Import, Export, Reset, and Reload are available in the header. Reload reads the saved workspace and discards pending edits; Reset replaces it with the default layout. DocSoft keeps the previous saved workspace in `workspace.json.bak`. If the current JSON is damaged, it preserves that file with a `.corrupt-*.json` suffix, restores the backup when possible, and shows a recovery notice. The DocSoft mark is shared by the window, executable, and tray icon; regenerate the icon assets with `python tools\Generate-DocSoftIcons.py` (Pillow required).

## Publish

Publish the self-contained Windows application and launcher:

```powershell
powershell -ExecutionPolicy Bypass -File tools\Publish-DockLauncher.ps1
```

The publish outputs are written under each AppHost project's `bin\Release\net9.0-windows\win-x64\publish` directory.

Create a Windows installer:

```powershell
powershell -ExecutionPolicy Bypass -File tools\Publish-DockLauncher.ps1 -Installer
```

Installer builds require Inno Setup 6 (`ISCC.exe`). The generated `DocSoft-Setup-<version>-win-x64.exe` is written to `artifacts\installer`. The installer keeps the existing AppId so it can upgrade older DockLauncher installations.

Download the latest ready-to-install version from [GitHub Releases](https://github.com/DemaNFox/DockLauncher/releases/latest).

## Test

```powershell
dotnet test src\DockLauncher.sln --no-build
```
