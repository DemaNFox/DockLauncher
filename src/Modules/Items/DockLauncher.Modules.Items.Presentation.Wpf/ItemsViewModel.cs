using DockLauncher.BuildingBlocks.Presentation.Wpf;

namespace DockLauncher.Modules.Items.Presentation.Wpf;

public sealed record LauncherItemCardViewModel(string DisplayName, string Type, string Target);

public sealed partial class ItemsViewModel : ViewModelBase
{
}