using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace DockLauncher.DragProbe;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly string _logDirectory;
    private string _reportText = "Drop something on the target area.";
    private string _lastCaptureText = "No captures yet";

    public MainWindow()
    {
        InitializeComponent();
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DockLauncher",
            "drag-probe");
        Directory.CreateDirectory(_logDirectory);
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string LogDirectoryText => _logDirectory;

    public string LastCaptureText
    {
        get => _lastCaptureText;
        private set
        {
            _lastCaptureText = value;
            OnPropertyChanged(nameof(LastCaptureText));
        }
    }

    public string ReportText
    {
        get => _reportText;
        private set
        {
            _reportText = value;
            OnPropertyChanged(nameof(ReportText));
        }
    }

    private void OnDragEvent(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = DragDropEffects.Copy;

        var captureId = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
        var captureDirectory = Path.Combine(_logDirectory, captureId);
        Directory.CreateDirectory(captureDirectory);

        var report = BuildReport(e, captureDirectory);
        var logPath = Path.Combine(captureDirectory, "drop-report.txt");
        File.WriteAllText(logPath, report, Encoding.UTF8);

        ReportText = report;
        LastCaptureText = captureId;
    }

    private string BuildReport(DragEventArgs e, string captureDirectory)
    {
        var builder = new StringBuilder();
        AppendHeader(builder, e, captureDirectory);
        AppendFormats(builder, e.Data, captureDirectory, autoConvert: false);
        AppendFormats(builder, e.Data, captureDirectory, autoConvert: true);
        AppendKnownFormats(builder, e.Data, captureDirectory);
        return builder.ToString();
    }

    private void AppendHeader(StringBuilder builder, DragEventArgs e, string captureDirectory)
    {
        var screenPoint = PointToScreen(e.GetPosition(this));
        builder.AppendLine("DockLauncher Drag Probe");
        builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
        builder.AppendLine($"Capture directory: {captureDirectory}");
        builder.AppendLine($"AllowedEffects: {e.AllowedEffects}");
        builder.AppendLine($"KeyStates: {e.KeyStates}");
        builder.AppendLine($"WindowPosition: {e.GetPosition(this)}");
        builder.AppendLine($"ScreenPosition: {screenPoint}");
        builder.AppendLine();
    }

    private void AppendFormats(StringBuilder builder, IDataObject dataObject, string captureDirectory, bool autoConvert)
    {
        builder.AppendLine(autoConvert ? "Formats (autoConvert=true)" : "Formats (autoConvert=false)");
        builder.AppendLine(new string('-', 80));

        var formats = SafeCall(() => dataObject.GetFormats(autoConvert), []);
        if (formats.Length == 0)
        {
            builder.AppendLine("No formats reported.");
            builder.AppendLine();
            return;
        }

        foreach (var format in formats.OrderBy(format => format, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"Format: {format}");
            builder.AppendLine($"  PresentExact: {SafeCall(() => dataObject.GetDataPresent(format, autoConvert), false)}");

            try
            {
                var value = dataObject.GetData(format, autoConvert);
                AppendValue(builder, format, value, captureDirectory);
            }
            catch (Exception exception)
            {
                builder.AppendLine($"  ReadError: {exception.GetType().Name}: {exception.Message}");
            }
        }

        builder.AppendLine();
    }

    private void AppendKnownFormats(StringBuilder builder, IDataObject dataObject, string captureDirectory)
    {
        builder.AppendLine("Known format probes");
        builder.AppendLine(new string('-', 80));

        var knownFormats = new[]
        {
            DataFormats.FileDrop,
            DataFormats.Text,
            DataFormats.UnicodeText,
            DataFormats.StringFormat,
            "Shell IDList Array",
            "FileGroupDescriptor",
            "FileGroupDescriptorW",
            "FileContents",
            "UniformResourceLocator",
            "UniformResourceLocatorW",
            "Preferred DropEffect",
            "Performed DropEffect",
            "Application User Model ID",
            "AppUserModelID",
            "DragImageBits"
        };

        foreach (var format in knownFormats)
        {
            builder.AppendLine($"Probe: {format}");
            builder.AppendLine($"  Present(false): {SafeCall(() => dataObject.GetDataPresent(format, false), false)}");
            builder.AppendLine($"  Present(true): {SafeCall(() => dataObject.GetDataPresent(format, true), false)}");
            try
            {
                var value = dataObject.GetData(format, true);
                AppendValue(builder, format, value, captureDirectory);
            }
            catch (Exception exception)
            {
                builder.AppendLine($"  ReadError: {exception.GetType().Name}: {exception.Message}");
            }
        }
    }

    private static void AppendValue(StringBuilder builder, string format, object? value, string captureDirectory)
    {
        if (value is null)
        {
            builder.AppendLine("  Value: <null>");
            return;
        }

        builder.AppendLine($"  Type: {value.GetType().FullName}");

        switch (value)
        {
            case string text:
                builder.AppendLine($"  TextLength: {text.Length}");
                builder.AppendLine($"  Text: {Escape(text)}");
                break;

            case string[] strings:
                builder.AppendLine($"  StringCount: {strings.Length}");
                foreach (var item in strings)
                {
                    builder.AppendLine($"    {item}");
                }
                break;

            case MemoryStream memoryStream:
                WriteStream(builder, format, memoryStream, captureDirectory);
                break;

            case Stream stream:
                WriteStream(builder, format, stream, captureDirectory);
                break;

            case byte[] bytes:
                WriteBytes(builder, format, bytes, captureDirectory);
                break;

            case int intValue:
                builder.AppendLine($"  Int32: {intValue}");
                break;

            default:
                builder.AppendLine($"  Value: {Escape(value.ToString() ?? string.Empty)}");
                if (Marshal.IsComObject(value))
                {
                    builder.AppendLine("  IsComObject: true");
                }

                break;
        }
    }

    private static void WriteStream(StringBuilder builder, string format, Stream stream, string captureDirectory)
    {
        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            WriteBytes(builder, format, memoryStream.ToArray(), captureDirectory);
        }
        catch (Exception exception)
        {
            builder.AppendLine($"  StreamReadError: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void WriteBytes(StringBuilder builder, string format, byte[] bytes, string captureDirectory)
    {
        var fileName = $"{SanitizeFileName(format)}-{Guid.NewGuid():N}.bin";
        var filePath = Path.Combine(captureDirectory, fileName);
        File.WriteAllBytes(filePath, bytes);

        builder.AppendLine($"  ByteLength: {bytes.Length}");
        builder.AppendLine($"  SavedBytes: {filePath}");
        builder.AppendLine($"  HexPreview: {ToHexPreview(bytes, 96)}");
        builder.AppendLine($"  Utf16Preview: {Escape(DecodePreview(bytes, Encoding.Unicode))}");
        builder.AppendLine($"  Utf8Preview: {Escape(DecodePreview(bytes, Encoding.UTF8))}");
    }

    private static string DecodePreview(byte[] bytes, Encoding encoding)
    {
        var count = Math.Min(bytes.Length, 512);
        return encoding.GetString(bytes, 0, count).Replace('\0', ' ');
    }

    private static string ToHexPreview(byte[] bytes, int maxBytes)
    {
        return string.Join(" ", bytes.Take(maxBytes).Select(value => value.ToString("X2")));
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "format" : sanitized;
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static T SafeCall<T>(Func<T> action, T fallback)
    {
        try
        {
            return action();
        }
        catch
        {
            return fallback;
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        ReportText = "Drop something on the target area.";
        LastCaptureText = "No captures yet";
    }

    private void OnOpenLogsClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _logDirectory,
            UseShellExecute = true
        });
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
