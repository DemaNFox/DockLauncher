# DockLauncher Troubleshooting

## Purpose

This file records build and project issues already encountered in this repository, plus the exact direction that helped resolve them. Check this file before debugging the same class of problem again.

## 2026-04-24: `dotnet build` looked broken with no clear error text

### Symptoms

- `dotnet build` ended with a short summary that effectively said build failed, while still showing `0 warnings` and `0 errors`.
- This happened even when project files looked valid.
- Some logs showed failures around project references or restore graph generation without a concrete compile error.

### Root cause

The build was running in a sandbox or offline environment and MSBuild/NuGet was not using the real local package cache. Because of that:

- restore tried to reach `https://api.nuget.org/v3/index.json`
- nested project builds failed during restore or reference resolution
- the short log output was misleading and often hid the real cause

### Extra factor

`src/Directory.Build.props` adds a global `PackageReference` to `Microsoft.Extensions.DependencyInjection.Abstractions` for all projects, so even very small projects still participate in package restore.

### How to diagnose

1. Run a minimal restore on a simple project.
2. If `NU1301` appears, suspect offline NuGet access first.
3. If short logs are empty or misleading, use `-v diag` and inspect the full log.
4. If artifacts exist but build output is unclear, check `bin/` and `obj/` timestamps.

### What worked

Point NuGet to the real on-disk package cache and ignore failed remote sources:

```powershell
$env:NUGET_PACKAGES='C:\Users\Nick\.nuget\packages'
dotnet build src\AppHost\DockLauncher.AppHost\DockLauncher.AppHost.csproj /p:RestoreIgnoreFailedSources=true -m:1 -v diag
```

### Result

- Build completed successfully.
- `DockLauncher.AppHost.exe` and `DockLauncher.AppHost.dll` were produced.

### Where to check next time

- `src/Directory.Build.props`
- local NuGet cache path: `C:\Users\Nick\.nuget\packages`
- large diagnostic build logs in repo root if present

## 2026-04-24: Compiler server connection message in build log

### Symptoms

Diagnostic build log contained:

`CompilerServer: server failed - cannot connect to the server`

### Meaning

This was not the real build failure. Roslyn fell back to normal compilation and the build still completed successfully afterward.

### Action

- Do not treat this line alone as the root cause.
- Always check the end of the log for the final build summary.

## 2026-04-24: Layout issue with extra scrollbars in configurator

### Symptoms

- `MainWindow` showed unnecessary global scrollbars.
- Scrollbars appeared around the whole configurator surface instead of only inside content-heavy sections.

### Root cause

`MainWindow.xaml` had a top-level `ScrollViewer` around the full working area. This forced scrolling at the shell level instead of at local panels.

### Fix

- Remove the outer `ScrollViewer`.
- Use a normal `Grid` for the main editor shell.
- Keep scrollbars only in local areas that can legitimately overflow.
- Raise `MinWidth` and `MinHeight` so the shell does not collapse into scrollbar-heavy layouts.

### Files

- `src/AppHost/DockLauncher.AppHost/MainWindow.xaml`

## 2026-04-24: Panel color picker had poor fit and unnecessary scroll

### Symptoms

- The panel color picker window showed cramped content and unwanted scrolling.

### Root cause

The dialog layout in `PanelColorPicker.cs` used an overall `ScrollViewer` around the main content instead of a stable two-column layout sized for the content.

### Fix

- Remove the outer `ScrollViewer`.
- Use a fixed grid layout for spectrum and controls.
- Increase minimum window size to match the actual content footprint.

### Files

- `src/AppHost/DockLauncher.AppHost/Dialogs/PanelColorPicker.cs`

## 2026-04-24: Text prompt dialog used an unnecessary outer scroll container

### Symptoms

- The text prompt dialog wrapped its whole content in a `ScrollViewer`.
- For normal prompt usage this made the window layout less stable than necessary and could introduce avoidable scrolling behavior.

### Root cause

`TextPromptWindow.xaml` used a global content scroll region instead of a direct stacked layout for title, message, input and actions.

### Fix

- Remove the outer `ScrollViewer`.
- Use a direct grid layout with separate rows for message, input and actions.
- Raise the minimum window size to the natural dialog footprint.

### Files

- `src/AppHost/DockLauncher.AppHost/Dialogs/TextPromptWindow.xaml`

## 2026-04-24: `Items` tab clipped controls after removing the shell-level scroll

### Symptoms

- The `Items` tab in the configurator stopped showing lower controls and part of the item board.
- Content was no longer globally scrollable, but there was also no local scroll area for the tall editor form.

### Root cause

After removing the outer configurator `ScrollViewer`, the `Items` tab still kept a tall fixed stack of controls above the item board. Since that form did not get its own scroll container, the lower part was clipped by the available tab height.

### Fix

- Keep the main configurator shell without a global scroll region.
- Add a local `ScrollViewer` only around the upper editor section of the `Items` tab.
- Keep the item board in its own star-sized row so it stays visible.
- Move selected item editing into the `Items` tab and remove the always-visible right-side inspector column.

### Files

- `src/AppHost/DockLauncher.AppHost/MainWindow.xaml`

## 2026-04-24: Dock panel showed an unnecessary internal scrollbar

### Symptoms

- Runtime dock panels could show a scrollbar even in normal cases where the panel should have fit on screen.
- The visual result looked broken, especially for horizontal docks.

### Root cause

The dock panel content always used `ScrollViewer` auto scroll based only on orientation. At the same time, the panel size calculation did not leave enough allowance for the panel chrome, so tiny overflow could trigger a scrollbar even when the panel was effectively supposed to fit.

### Fix

- Recalculate panel metrics with a small chrome allowance.
- Clamp requested panel size against the monitor work area before window creation.
- Enable horizontal or vertical internal scrolling only when the content truly exceeds the available work area.

### Files

- `src/AppHost/DockLauncher.AppHost/Docking/DockShellCoordinator.cs`
- `src/AppHost/DockLauncher.AppHost/Docking/DockPanelWindowViewModel.cs`

## 2026-04-24: `Edit Item` dialog could crash when opened from the dock panel

### Symptoms

- Right-clicking a dock item and choosing `Edit Item` could terminate the app or fail before the dialog appeared.

### Root cause

The new dialog was opened modally while the window XAML still requested `WindowStartupLocation="CenterOwner"`. That is fragile when the dialog is launched from runtime dock windows that do not behave like a normal owned configurator window.

### Fix

- Stop centering the dialog relative to an owner-only startup mode.
- Use the same simple modal opening pattern as the working text prompt dialog.
- Wrap runtime edit invocation with an error message so future failures surface a concrete exception instead of a silent crash.

### Files

- `src/AppHost/DockLauncher.AppHost/Dialogs/ItemEditorWindow.xaml`
- `src/AppHost/DockLauncher.AppHost/Dialogs/ItemEditorService.cs`
- `src/AppHost/DockLauncher.AppHost/Docking/DockShellCoordinator.cs`

## 2026-06-30: Topmost dock could fall behind normal windows and group flyouts had unused width

### Symptoms

- A panel with `Always On Top` enabled could still be covered by another application window.
- A tile group with nine items rendered as a three-column grid inside a fixed-width flyout, leaving an empty strip on the right.

### Root cause

- The panel relied only on WPF's `Topmost` dependency property. Its native topmost Z-order was not reaffirmed after the HWND was created or when runtime visibility was synchronized.
- Group flyout width was hard-coded to 420/460 px while tile row calculations assumed a fixed column count regardless of the grid's useful width.

### Fix

- Apply `SetWindowPos(HWND_TOPMOST)` with non-activating flags after HWND initialization and during existing runtime state synchronization.
- Evaluate every column count that physically fits within 90% of the work area and score candidates by aspect ratio, empty cells, last-row quality, and excessive elongation.
- Prefer a complete layout; only use a height-bounded vertically scrolling layout when no complete candidate fits.
- Give the selected column count and exact grid width to the WPF `UniformGrid`, so control chrome cannot cause an unintended second wrap.
- Place the divider between the group header and the flyout content.

### Files

- `src/AppHost/DockLauncher.AppHost/Docking/DockPanelWindow.xaml.cs`
- `src/AppHost/DockLauncher.AppHost/Docking/GroupFlyoutWindow.xaml`
- `src/AppHost/DockLauncher.AppHost/Docking/GroupFlyoutWindowViewModel.cs`
- `src/Tests/DockLauncher.UiSmoke.Tests/AppShellTests.cs`

## 2026-07-01: Undocked side panel kept its docked height

### Symptoms

- Dragging a panel away from the left or right edge produced an extremely tall floating panel with a large empty area.
- Restarting the application could preserve the oversized panel.

### Root cause

The drag handler persisted the docked window's current `ActualWidth` and `ActualHeight` as the floating panel's custom size. When an implicitly vertical side panel became horizontal in floating mode, its long primary-axis height became the horizontal panel's cross-axis height.

### Fix

- Do not carry custom window dimensions across a docked-to-floating transition; let floating metrics recalculate from the items.
- Clamp a panel's cross-axis dimension to its content-derived expanded size, which also repairs previously persisted oversized floating panels.
- Continue preserving explicit dimensions when an already-floating panel is moved or resized.

### Files

- `src/AppHost/DockLauncher.AppHost/Docking/DockPanelWindow.xaml.cs`
- `src/AppHost/DockLauncher.AppHost/Docking/DockShellCoordinator.cs`
- `src/Tests/DockLauncher.UiSmoke.Tests/AppShellTests.cs`

## 2026-07-10: `Open Location` appeared for virtual items and could leave Windows busy

### Symptoms

- Panel groups and built-in actions displayed `Open Location` even though they have no filesystem location.
- Opening locations for multiple files could intermittently fail, after which the Windows busy cursor could remain visible until DockLauncher exited.

### Root cause

- The menu visibility used the generic launcher visibility, which only excluded separators and did not distinguish filesystem items from `group:`, `profile:`, `action:`, URL, or command targets.
- Location opening launched a synthetic folder item through the general shell-association service. Re-entering the Windows shell for an already running Explorer instance was unnecessary and could leave the WPF async command tied to unstable shell activation behavior.

### Fix

- Expose `Open Location` for filesystem-capable targets, including physical command scripts such as `.bat`, and reject virtual target prefixes.
- Start `explorer.exe` directly without shell execution. Open folders normally and use `/select` for files, then immediately dispose the returned process handle and complete the UI command.
- Treat missing, stale, or temporarily unavailable targets as a completed no-op.

### Files

- `src/AppHost/DockLauncher.AppHost/Docking/DockPanelWindowViewModel.cs`
- `src/AppHost/DockLauncher.AppHost/Docking/GroupFlyoutWindowViewModel.cs`
- `src/AppHost/DockLauncher.AppHost/Docking/DockPanelWindow.xaml`
- `src/AppHost/DockLauncher.AppHost/Docking/GroupFlyoutWindow.xaml`
- `src/AppHost/DockLauncher.AppHost/Docking/DockShellCoordinator.cs`
- `src/Tests/DockLauncher.UiSmoke.Tests/AppShellTests.cs`

## Rule For New Issues

When a new issue is resolved:

1. Add a new dated entry here.
2. Record symptoms, root cause, exact fix, and key files.
3. Prefer command lines or concrete reproduction notes over vague summaries.
