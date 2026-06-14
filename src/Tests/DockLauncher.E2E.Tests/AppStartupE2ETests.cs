using System.Diagnostics;
using System.Windows.Automation;
using DockLauncher.BuildingBlocks.Infrastructure.FileSystem;
using FluentAssertions;

namespace DockLauncher.E2E.Tests;

[Collection("DockLauncher E2E")]
public sealed class AppStartupE2ETests
{
    [Fact]
    public async Task FirstRun_ShouldCreateWorkspaceAndOpenConfigurator()
    {
        var appExe = ResolveAppExe();
        var dataRoot = Path.Combine(Path.GetTempPath(), "DockLauncher.E2E", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);

        using var process = StartApp(appExe, dataRoot);
        try
        {
            await WaitUntilAsync(
                () => File.Exists(Path.Combine(dataRoot, "workspace.json")),
                TimeSpan.FromSeconds(15),
                "workspace.json was not created.");

            await WaitUntilAsync(
                () => File.Exists(Path.Combine(dataRoot, "first-run-complete.txt")),
                TimeSpan.FromSeconds(15),
                "first-run marker was not created.");

            var configurator = await WaitForWindowAsync(process.Id, "DockLauncher Configurator", TimeSpan.FromSeconds(15));

            configurator.Should().NotBeNull();
            process.HasExited.Should().BeFalse();
        }
        finally
        {
            KillProcess(process);
            TryDeleteDirectory(dataRoot);
        }
    }

    private static string ResolveAppExe()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "DockLauncher.exe"),
            Path.Combine(baseDirectory, "DockLauncher.AppHost.exe")
        };

        var appExe = candidates.FirstOrDefault(File.Exists);
        appExe.Should().NotBeNull("the AppHost project reference should copy the runnable exe to the test output");
        return appExe!;
    }

    private static Process StartApp(string appExe, string dataRoot)
    {
        var startInfo = new ProcessStartInfo(appExe)
        {
            WorkingDirectory = Path.GetDirectoryName(appExe)!,
            UseShellExecute = false
        };
        startInfo.Environment[AppDataPaths.RootOverrideEnvironmentVariable] = dataRoot;

        var process = Process.Start(startInfo);
        process.Should().NotBeNull();
        return process!;
    }

    private static async Task<AutomationElement?> WaitForWindowAsync(int processId, string title, TimeSpan timeout)
    {
        AutomationElement? window = null;
        await WaitUntilAsync(
            () =>
            {
                window = FindWindow(processId, title);
                return window is not null;
            },
            timeout,
            $"Window '{title}' was not found.");

        return window;
    }

    private static AutomationElement? FindWindow(int processId, string title)
    {
        var processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
        var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, processCondition);
        return windows
            .Cast<AutomationElement>()
            .FirstOrDefault(window => string.Equals(window.Current.Name, title, StringComparison.Ordinal));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string failureMessage)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(150);
        }

        throw new TimeoutException(failureMessage);
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

[CollectionDefinition("DockLauncher E2E", DisableParallelization = true)]
public sealed class DockLauncherE2ECollection;
