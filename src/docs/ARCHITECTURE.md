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