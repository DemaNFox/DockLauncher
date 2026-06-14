using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using DockLauncher.Modules.Items.Domain;

namespace DockLauncher.Modules.Items.Application;

public static class LauncherItemDraftFactory
{
    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe",
        ".com"
    };

    private static readonly HashSet<string> CommandExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bat",
        ".cmd",
        ".ps1"
    };

    public static LauncherItemDraft Create(string target)
    {
        var normalizedTarget = target.Trim();

        if (BuiltInDockActions.TryResolve(normalizedTarget, out var resolvedAction))
        {
            return new LauncherItemDraft(resolvedAction.DisplayName, LauncherItemType.Action, normalizedTarget);
        }

        if (Uri.TryCreate(normalizedTarget, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return new LauncherItemDraft(uri.Host, LauncherItemType.Url, normalizedTarget);
        }

        if (Directory.Exists(normalizedTarget))
        {
            return new LauncherItemDraft(Path.GetFileName(normalizedTarget.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), LauncherItemType.Folder, normalizedTarget);
        }

        var extension = Path.GetExtension(normalizedTarget);
        var displayName = Path.GetFileNameWithoutExtension(normalizedTarget);

        if (string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase))
        {
            if (TryResolveShortcut(normalizedTarget, out var shortcutTarget, out var shortcutArguments))
            {
                var resolvedDraft = Create(shortcutTarget);
                return new LauncherItemDraft(
                    string.IsNullOrWhiteSpace(displayName) ? resolvedDraft.DisplayName : displayName,
                    resolvedDraft.Type,
                    resolvedDraft.Target,
                    string.IsNullOrWhiteSpace(shortcutArguments) ? resolvedDraft.Arguments : shortcutArguments);
            }

            return new LauncherItemDraft(displayName, LauncherItemType.Shortcut, normalizedTarget);
        }

        if (ExecutableExtensions.Contains(extension))
        {
            return new LauncherItemDraft(displayName, LauncherItemType.Application, normalizedTarget);
        }

        if (CommandExtensions.Contains(extension))
        {
            return new LauncherItemDraft(displayName, LauncherItemType.Command, normalizedTarget);
        }

        return new LauncherItemDraft(string.IsNullOrWhiteSpace(displayName) ? normalizedTarget : displayName, LauncherItemType.File, normalizedTarget);
    }

    private static bool TryResolveShortcut(string shortcutPath, out string targetPath, out string? arguments)
    {
        targetPath = string.Empty;
        arguments = null;

        if (!File.Exists(shortcutPath))
        {
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        object? shell = null;
        object? shortcut = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return false;
            }

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return false;
            }

            shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [shortcutPath]);
            if (shortcut is null)
            {
                return false;
            }

            var shortcutType = shortcut.GetType();
            var resolvedTarget = shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
            if (string.IsNullOrWhiteSpace(resolvedTarget))
            {
                return false;
            }

            targetPath = Environment.ExpandEnvironmentVariables(resolvedTarget.Trim());
            arguments = shortcutType.InvokeMember("Arguments", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
            arguments = string.IsNullOrWhiteSpace(arguments) ? null : arguments.Trim();
            return true;
        }
        catch (COMException)
        {
            return false;
        }
        catch (TargetInvocationException)
        {
            return false;
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? instance)
    {
        if (OperatingSystem.IsWindows() && instance is not null && Marshal.IsComObject(instance))
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }
}

public sealed record LauncherItemDraft(string DisplayName, LauncherItemType Type, string Target, string? Arguments = null);
