# DockLauncher Project Map

## Purpose

This file is a quick orientation map for the repository: what lives where, why it exists, and where to look first for common task types.

## Top Level

- `src/`
  Main solution root.
- `tools/`
  Helper scripts for local workflows.
- `README.md`
  High-level project summary and startup notes.
- `AGENT.md`
  Working rules for agents and contributors in this repository.

## Solution Layout

- `src/AppHost/`
  Application entry point, WPF shell, dialogs, runtime windows, DI composition, startup.
- `src/BuildingBlocks/`
  Shared abstractions and reusable cross-cutting code.
- `src/Modules/`
  Feature modules split by business area.
- `src/Integrations/`
  External and platform-specific adapters, mostly Windows-related.
- `src/Tests/`
  Automated tests mirroring the main architecture.
- `src/docs/`
  Internal documentation and troubleshooting notes.

## AppHost

Path: `src/AppHost/DockLauncher.AppHost`

Look here for:

- WPF application startup: `App.xaml`, `Program.cs`
- Main configurator window: `MainWindow.xaml`, `MainWindow.xaml.cs`
- Runtime dock windows:
  `Docking/DockPanelWindow.xaml`
  `Docking/GroupFlyoutWindow.xaml`
  `Docking/FolderFlyoutWindow.xaml`
- Runtime dock orchestration:
  `Docking/DockShellCoordinator.cs`
- Dialogs and pickers:
  `Dialogs/`
- Shared visual resources for the app host:
  `Themes/`
- Window positioning and screen-fit logic:
  `Configuration/WindowDisplayPolicy.cs`

## BuildingBlocks

Path: `src/BuildingBlocks`

Projects:

- `DockLauncher.BuildingBlocks.Domain`
  Base domain-level contracts and primitives.
- `DockLauncher.BuildingBlocks.Application`
  Shared application-layer contracts and helpers.
- `DockLauncher.BuildingBlocks.Infrastructure`
  Shared infrastructure utilities.
- `DockLauncher.BuildingBlocks.Presentation.Wpf`
  Shared WPF presentation helpers and theme keys.

## Modules

Path: `src/Modules`

Each feature generally follows the same split:

- `*.Domain`
  Entities, enums, domain rules.
- `*.Application`
  Use cases, services, contracts.
- `*.Infrastructure`
  Persistence, adapters, integration glue.
- `*.Presentation.Wpf`
  WPF-facing view models or presentation helpers when needed.

Current feature areas:

- `Panels`
  Panel layout, appearance, docking options.
- `Items`
  Launcher items and item management.
- `Settings`
  Workspace editing, persisted settings, main editor view model.
- `Groups`
  Grouped launcher item behavior.
- `LaunchProfiles`
  Item launch sequences and profiles.
- `FolderFlyouts`
  Folder entry modeling and behavior.
- `Icons`
  Icon loading and icon-related services.
- `Tray`
  Tray interactions.
- `Hotkeys`
  Global hotkey behavior.
- `ShellIntegration`
  Shell-level integration points.

## Most Important Files By Task

### If the task is about window layout or visual issues

- `src/AppHost/DockLauncher.AppHost/MainWindow.xaml`
- `src/AppHost/DockLauncher.AppHost/Docking/*.xaml`
- `src/AppHost/DockLauncher.AppHost/Themes/*.xaml`
- `src/AppHost/DockLauncher.AppHost/Dialogs/PanelColorPicker.cs`

### If the task is about workspace editing behavior

- `src/Modules/Settings/DockLauncher.Modules.Settings.Presentation.Wpf/WorkspaceEditorViewModel.cs`
- `src/AppHost/DockLauncher.AppHost/MainWindow.xaml.cs`

### If the task is about runtime dock size, spacing, labels, panel appearance

- `src/AppHost/DockLauncher.AppHost/Docking/DockPanelWindowViewModel.cs`
- `src/AppHost/DockLauncher.AppHost/Docking/DockPanelWindow.xaml`
- `src/Modules/Panels/`

### If the task is about dependency injection or app startup

- `src/AppHost/DockLauncher.AppHost/Program.cs`
- `src/AppHost/DockLauncher.AppHost/Hosting/HostBuilderFactory.cs`
- `src/AppHost/DockLauncher.AppHost/Composition/ServiceCollectionExtensions.cs`

### If the task is about persistence and saved workspace state

- `src/Modules/Settings/DockLauncher.Modules.Settings.Infrastructure/`
- `src/Modules/Settings/DockLauncher.Modules.Settings.Domain/`

## Build Notes

- Central package versions are managed in `src/Directory.Packages.props`.
- A shared package reference is injected globally from `src/Directory.Build.props`.
- Because of that shared package reference, even simple projects may require NuGet resolution.

## Related Docs

- `src/docs/ARCHITECTURE.md`
- `src/docs/TROUBLESHOOTING.md`
- `src/docs/RUNTIME_PANEL_WORKFLOW.md`
- `AGENT.md`
