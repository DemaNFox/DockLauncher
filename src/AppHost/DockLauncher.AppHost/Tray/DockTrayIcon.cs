using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using DockLauncher.BuildingBlocks.Application.Contracts;
using H.NotifyIcon;

namespace DockLauncher.AppHost.Tray;

public sealed class DockTrayIcon : IDisposable
{
    private readonly IDockShellController _dockShellController;
    private TaskbarIcon? _taskbarIcon;

    public DockTrayIcon(IDockShellController dockShellController)
    {
        _dockShellController = dockShellController;
    }

    public void Initialize()
    {
        if (_taskbarIcon is not null)
        {
            return;
        }

        var contextMenu = new ContextMenu();

        var openConfigurator = new MenuItem { Header = "Open Configurator" };
        openConfigurator.Click += (_, _) => _dockShellController.ShowConfigurator();
        contextMenu.Items.Add(openConfigurator);

        var togglePanels = new MenuItem { Header = "Show / Hide Panels" };
        togglePanels.Click += (_, _) => _dockShellController.TogglePanelsVisibility();
        contextMenu.Items.Add(togglePanels);

        var showHiddenPanels = new MenuItem { Header = "Show Hidden Panels" };
        showHiddenPanels.Click += (_, _) => _dockShellController.ShowHiddenPanels();
        contextMenu.Items.Add(showHiddenPanels);

        var refreshPanels = new MenuItem { Header = "Refresh Panels" };
        refreshPanels.Click += async (_, _) => await _dockShellController.RefreshAsync();
        contextMenu.Items.Add(refreshPanels);

        contextMenu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit DockLauncher" };
        exitItem.Click += (_, _) => _dockShellController.Exit();
        contextMenu.Items.Add(exitItem);

        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "DockLauncher",
            ContextMenu = contextMenu,
            IconSource = new GeneratedIconSource
            {
                Text = "D",
                FontSize = 54,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                Foreground = new SolidColorBrush(Color.FromRgb(244, 247, 250)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(75, 85, 99)),
                BorderThickness = 6
            }
        };

        _taskbarIcon.TrayLeftMouseUp += (_, _) => _dockShellController.EnsurePanelsVisible();
        _taskbarIcon.ForceCreate();
    }

    public void Dispose()
    {
        _taskbarIcon?.Dispose();
        _taskbarIcon = null;
    }
}
