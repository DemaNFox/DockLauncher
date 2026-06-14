# Runtime Panel Workflow

## Purpose

This note describes the intended split between the runtime dock panel and the configurator after the shell UX simplification work.

## Core idea

Use the runtime panel for everyday item work.

Use the configurator for panel structure, appearance, workspace-level entities and bulk editing.

When the configurator is open, runtime panel edits are applied to the configurator's active workspace copy and then previewed in the dock. This keeps both editing surfaces synchronized and prevents one surface from overwriting the other with stale workspace state.

## Runtime panel responsibilities

### Empty panel context menu

- Add file
- Add folder
- Create group in configurator
- Create launch profile in configurator
- Open configurator
- panel-level commands like rename, duplicate, hide, lock, appearance toggles

### Item context menu

- Launch
- Launch as administrator
- Edit item
- Open location
- Rename
- Move to panel
- Duplicate item
- Duplicate to new panel
- Remove from panel

### Item editing dialog

The item dialog is now the main place for:

- display name
- target for direct file/folder/command/url items
- launch arguments
- run as administrator flag

For structured targets like `group:`, `profile:` or built-in `action:` items, the dialog allows label and launch flag changes, but target wiring stays owned by the configurator.

## Flyouts

Group and folder flyouts close when focus moves outside the flyout window, including a click on another screen area.
Opening a new group or folder flyout closes the previously open flyout immediately, so switching between groups or folders takes one click.
Flyout lists show item names only; long names are truncated with an ellipsis instead of exposing full file paths in the main list.

## Configurator responsibilities

### Keep in configurator

- panel list and selection
- panel design and layout
- groups management
- launch profiles management
- import/export/save/reload/reset
- overview of items on selected panel

### Avoid using configurator for

- routine file/folder addition
- routine item rename
- routine launch argument edits
- routine admin launch flag changes

## Groups and launch profiles

Groups and launch profiles are no longer supposed to depend on the old `SelectedPanelItem` workflow.

### Groups

- choose a source panel inside the `Groups` tab
- drag launcher items from that panel-specific source list into the selected group
- drop files or folders from Windows directly onto the selected group area
- add the finished group launcher to a panel only when needed

### Launch profiles

- choose a source panel inside the `Launch Profiles` tab
- drag launcher items from that panel-specific source list into the selected profile
- drop files or folders from Windows directly onto the profile steps area
- dropped items become workspace items and then profile steps
- step metadata like delay and admin flag stays on the profile side

### Persistence rule

Workspace items referenced by groups or launch profiles must be saved even when they are not currently assigned to any panel.

## File map

- `src/AppHost/DockLauncher.AppHost/Docking/DockPanelWindow.xaml`
- `src/AppHost/DockLauncher.AppHost/Docking/DockPanelWindowViewModel.cs`
- `src/AppHost/DockLauncher.AppHost/Docking/DockShellCoordinator.cs`
- `src/AppHost/DockLauncher.AppHost/Dialogs/ItemEditorWindow.xaml`
- `src/AppHost/DockLauncher.AppHost/MainWindow.xaml`
