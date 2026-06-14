using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace DockLauncher.AppHost.DragDrop;

public static class DroppedPathExtractor
{
    private const string FileGroupDescriptorWFormat = "FileGroupDescriptorW";
    private const string FileContentsFormat = "FileContents";
    private const string FileNameWFormat = "FileNameW";

    public static bool HasPaths(IDataObject data)
    {
        return data.GetDataPresent(DataFormats.FileDrop)
            || data.GetDataPresent(FileNameWFormat)
            || (data.GetDataPresent(FileGroupDescriptorWFormat) && data.GetDataPresent(FileContentsFormat));
    }

    public static IReadOnlyList<string> ExtractPaths(IDataObject data)
    {
        if (data.GetData(DataFormats.FileDrop) is string[] fileDropPaths)
        {
            return fileDropPaths;
        }

        if (TryGetFileNamePaths(data, out var fileNamePaths))
        {
            return fileNamePaths;
        }

        if (TrySaveVirtualFile(data, out var virtualFilePath))
        {
            return [virtualFilePath];
        }

        return [];
    }

    private static bool TryGetFileNamePaths(IDataObject data, out IReadOnlyList<string> paths)
    {
        paths = [];
        if (!data.GetDataPresent(FileNameWFormat))
        {
            return false;
        }

        var raw = data.GetData(FileNameWFormat);
        paths = raw switch
        {
            string path when !string.IsNullOrWhiteSpace(path) => [path],
            string[] values => values.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray(),
            _ => []
        };

        return paths.Count > 0;
    }

    private static bool TrySaveVirtualFile(IDataObject data, out string savedPath)
    {
        savedPath = string.Empty;
        if (!data.GetDataPresent(FileGroupDescriptorWFormat) || !data.GetDataPresent(FileContentsFormat))
        {
            return false;
        }

        var fileName = TryReadFirstFileDescriptorName(data) ?? $"shortcut-{Guid.NewGuid():N}.lnk";
        var fileContents = data.GetData(FileContentsFormat);
        if (fileContents is not Stream stream)
        {
            return false;
        }

        var targetDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DockLauncher",
            "shortcuts",
            "virtual-drops");
        Directory.CreateDirectory(targetDirectory);

        savedPath = Path.Combine(targetDirectory, CreateUniqueSafeFileName(targetDirectory, fileName));
        using var output = File.Create(savedPath);
        stream.CopyTo(output);
        return true;
    }

    private static string? TryReadFirstFileDescriptorName(IDataObject data)
    {
        if (data.GetData(FileGroupDescriptorWFormat) is not MemoryStream descriptorStream)
        {
            return null;
        }

        var descriptorBytes = descriptorStream.ToArray();
        var descriptorOffset = sizeof(uint);
        var descriptorSize = Marshal.SizeOf<FileDescriptorW>();
        if (descriptorBytes.Length < descriptorOffset + descriptorSize)
        {
            return null;
        }

        var handle = GCHandle.Alloc(descriptorBytes, GCHandleType.Pinned);
        try
        {
            var pointer = IntPtr.Add(handle.AddrOfPinnedObject(), descriptorOffset);
            var descriptor = Marshal.PtrToStructure<FileDescriptorW>(pointer);
            return string.IsNullOrWhiteSpace(descriptor.FileName) ? null : descriptor.FileName;
        }
        finally
        {
            handle.Free();
        }
    }

    private static string CreateUniqueSafeFileName(string directory, string fileName)
    {
        var safeFileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = $"shortcut-{Guid.NewGuid():N}.lnk";
        }

        var extension = Path.GetExtension(safeFileName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(safeFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".lnk";
        }

        var candidate = $"{nameWithoutExtension}{extension}";
        var counter = 1;
        while (File.Exists(Path.Combine(directory, candidate)))
        {
            candidate = $"{nameWithoutExtension}-{counter}{extension}";
            counter++;
        }

        return candidate;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileDescriptorW
    {
        public uint Flags;
        public Guid ClassId;
        public int SizeX;
        public int SizeY;
        public int PointX;
        public int PointY;
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint FileSizeHigh;
        public uint FileSizeLow;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string FileName;
    }
}
