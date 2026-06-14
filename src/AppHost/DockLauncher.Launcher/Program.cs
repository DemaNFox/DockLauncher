using System.Diagnostics;
using System.IO;
using System.Windows;

namespace DockLauncher.Launcher;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var appDirectory = Path.Combine(baseDirectory, "app");
        var appExecutable = Path.Combine(appDirectory, "DockLauncher.exe");

        if (!File.Exists(appExecutable))
        {
            MessageBox.Show(
                $"Application files were not found:\n{appExecutable}",
                "DockLauncher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = appExecutable,
            WorkingDirectory = appDirectory,
            UseShellExecute = true
        });
    }
}
