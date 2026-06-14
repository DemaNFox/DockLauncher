using System.Collections.ObjectModel;
using DockLauncher.Modules.Items.Domain;

namespace DockLauncher.Modules.Items.Application;

public static class BuiltInDockActions
{
    private static readonly ReadOnlyCollection<BuiltInDockActionPreset> PresetsInternal = new(
    [
        new("Lock Screen", "action:lock", "rundll32.exe", "user32.dll,LockWorkStation"),
        new("Sign Out", "action:signout", "shutdown.exe", "/l"),
        new("Sleep", "action:sleep", "rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0"),
        new("Restart", "action:restart", "shutdown.exe", "/r /t 0"),
        new("Shut Down", "action:shutdown", "shutdown.exe", "/s /t 0")
    ]);

    public static IReadOnlyList<BuiltInDockActionPreset> Presets => PresetsInternal;

    public static bool TryResolve(LauncherItem item, out ResolvedDockAction resolvedAction)
    {
        if (item.Type != LauncherItemType.Action)
        {
            resolvedAction = null!;
            return false;
        }

        return TryResolve(item.Target, out resolvedAction);
    }

    public static bool TryResolve(string target, out ResolvedDockAction resolvedAction)
    {
        var preset = Presets.FirstOrDefault(candidate => string.Equals(candidate.Target, target, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
        {
            resolvedAction = null!;
            return false;
        }

        resolvedAction = new ResolvedDockAction(preset.DisplayName, preset.FileName, preset.Arguments);
        return true;
    }
}

public sealed record BuiltInDockActionPreset(string DisplayName, string Target, string FileName, string Arguments);

public sealed record ResolvedDockAction(string DisplayName, string FileName, string Arguments);
