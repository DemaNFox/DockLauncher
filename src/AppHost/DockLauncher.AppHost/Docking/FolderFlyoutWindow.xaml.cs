using System;
using System.Windows;
using DockLauncher.AppHost.Configuration;

namespace DockLauncher.AppHost.Docking;

public partial class FolderFlyoutWindow : Window
{
    public FolderFlyoutWindow(FolderFlyoutWindowViewModel viewModel)
    {
        InitializeComponent();
        WindowDisplayPolicy.Apply(this);
        DataContext = viewModel;
        Loaded += OnLoaded;
        Deactivated += OnDeactivated;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is FolderFlyoutWindowViewModel viewModel)
        {
            Left = viewModel.Left;
            Top = viewModel.Top;
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        Close();
    }
}
