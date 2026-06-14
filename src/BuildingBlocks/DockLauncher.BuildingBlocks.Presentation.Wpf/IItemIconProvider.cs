using DockLauncher.Modules.Items.Domain;
using System.Windows.Media;

namespace DockLauncher.BuildingBlocks.Presentation.Wpf;

public interface IItemIconProvider
{
    ImageSource? GetIcon(LauncherItemType type, string target, string? iconPath = null);
}
