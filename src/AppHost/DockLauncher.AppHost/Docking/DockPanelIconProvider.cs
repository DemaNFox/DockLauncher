using System.IO;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockLauncher.BuildingBlocks.Presentation.Wpf;
using DockLauncher.Modules.Items.Domain;

namespace DockLauncher.AppHost.Docking;

public interface IDockPanelIconProvider : IItemIconProvider
{
}

public sealed class DockPanelIconProvider : IDockPanelIconProvider
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint ShgfiSysIconIndex = 0x000004000;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeNormal = 0x00000080;
    private const int ShilExtraLarge = 0x2;
    private const int ShilJumbo = 0x4;

    private readonly ConcurrentDictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ImageSource? GetIcon(LauncherItemType type, string target, string? iconPath = null)
    {
        var cacheKey = $"{type}:{target}:{iconPath}";
        return _cache.GetOrAdd(cacheKey, _ => ResolveIcon(type, target, iconPath));
    }

    private static ImageSource? ResolveIcon(LauncherItemType type, string target, string? iconPath)
    {
        if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
        {
            return LoadBitmapIcon(iconPath);
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        if (type is LauncherItemType.Action or LauncherItemType.Separator)
        {
            return null;
        }

        if (type == LauncherItemType.Url)
        {
            return GetShellIcon(".url", FileAttributeNormal, useFileAttributes: true);
        }

        if (type == LauncherItemType.Folder)
        {
            return GetShellIcon(target, FileAttributeDirectory, useFileAttributes: !Directory.Exists(target));
        }

        var iconTarget = TryResolveShortcutTarget(target, out var shortcutTarget) ? shortcutTarget : target;
        return GetShellIcon(iconTarget, FileAttributeNormal, useFileAttributes: !File.Exists(iconTarget));
    }

    private static ImageSource? LoadBitmapIcon(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            var iconTarget = TryResolveShortcutTarget(path, out var shortcutTarget) ? shortcutTarget : path;
            return GetShellIcon(iconTarget, FileAttributeNormal, useFileAttributes: false);
        }
    }

    private static ImageSource? GetShellIcon(string path, uint attributes, bool useFileAttributes)
    {
        var imageListIcon = GetShellImageListIcon(path, attributes, useFileAttributes, ShilExtraLarge)
            ?? GetShellImageListIcon(path, attributes, useFileAttributes, ShilJumbo);
        if (imageListIcon is not null)
        {
            return imageListIcon;
        }

        var flags = ShgfiIcon | ShgfiLargeIcon;
        if (useFileAttributes)
        {
            flags |= ShgfiUseFileAttributes;
        }

        var fileInfo = new ShFileInfo();
        var result = SHGetFileInfo(path, attributes, out fileInfo, (uint)Marshal.SizeOf<ShFileInfo>(), flags);
        if (result == IntPtr.Zero || fileInfo.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(
                fileInfo.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
        finally
        {
            DestroyIcon(fileInfo.hIcon);
        }
    }

    private static ImageSource? GetShellImageListIcon(string path, uint attributes, bool useFileAttributes, int imageListSize)
    {
        var flags = ShgfiSysIconIndex;
        if (useFileAttributes)
        {
            flags |= ShgfiUseFileAttributes;
        }

        var fileInfo = new ShFileInfo();
        var result = SHGetFileInfo(path, attributes, out fileInfo, (uint)Marshal.SizeOf<ShFileInfo>(), flags);
        if (result == IntPtr.Zero || fileInfo.iIcon < 0)
        {
            return null;
        }

        var imageListId = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
        if (SHGetImageList(imageListSize, ref imageListId, out var imageList) != 0 || imageList is null)
        {
            return null;
        }

        var hIcon = IntPtr.Zero;
        try
        {
            if (imageList.GetIcon(fileInfo.iIcon, 0, ref hIcon) != 0 || hIcon == IntPtr.Zero)
            {
                return null;
            }

            var image = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hIcon != IntPtr.Zero)
            {
                DestroyIcon(hIcon);
            }

            Marshal.ReleaseComObject(imageList);
        }
    }

    private static bool TryResolveShortcutTarget(string path, out string targetPath)
    {
        targetPath = string.Empty;
        if (!OperatingSystem.IsWindows()
            || !string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path))
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
            shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [path]);
            var resolvedTarget = shortcut?.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null) as string;
            if (string.IsNullOrWhiteSpace(resolvedTarget))
            {
                return false;
            }

            targetPath = Environment.ExpandEnvironmentVariables(resolvedTarget.Trim());
            return true;
        }
        catch
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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        out ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IImageList? ppv);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig]
        int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);

        [PreserveSig]
        int ReplaceIcon(int i, IntPtr hicon, ref int pi);

        [PreserveSig]
        int SetOverlayImage(int iImage, int iOverlay);

        [PreserveSig]
        int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);

        [PreserveSig]
        int AddMasked(IntPtr hbmImage, int crMask, ref int pi);

        [PreserveSig]
        int Draw(IntPtr pimldp);

        [PreserveSig]
        int Remove(int i);

        [PreserveSig]
        int GetIcon(int i, int flags, ref IntPtr picon);
    }
}
