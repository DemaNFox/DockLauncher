using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DockLauncher.AppHost.Configuration;

internal sealed record WindowDisplayPolicyOptions(
    double MaxWidthRatio = 0.9,
    double MaxHeightRatio = 0.9,
    bool ClampLocation = true,
    bool RecenterOnLoad = false);

internal static class WindowDisplayPolicy
{
    public static void Apply(Window window, WindowDisplayPolicyOptions? options = null)
    {
        options ??= new WindowDisplayPolicyOptions();

        var state = new WindowDisplayPolicyState(window, options);
        window.SourceInitialized += state.OnSourceInitialized;
        window.Loaded += state.OnLoaded;
        window.SizeChanged += state.OnSizeChanged;
        window.LocationChanged += state.OnLocationChanged;
        window.Closed += state.OnClosed;
    }

    private sealed class WindowDisplayPolicyState
    {
        private readonly Window _window;
        private readonly WindowDisplayPolicyOptions _options;
        private bool _isApplying;

        public WindowDisplayPolicyState(Window window, WindowDisplayPolicyOptions options)
        {
            _window = window;
            _options = options;
        }

        public void OnSourceInitialized(object? sender, EventArgs e)
        {
            ApplyBounds(clampLocation: false, recenter: false);
        }

        public void OnLoaded(object? sender, RoutedEventArgs e)
        {
            ApplyBounds(clampLocation: _options.ClampLocation, recenter: _options.RecenterOnLoad);
        }

        public void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            ApplyBounds(clampLocation: _options.ClampLocation, recenter: false);
        }

        public void OnLocationChanged(object? sender, EventArgs e)
        {
            ApplyBounds(clampLocation: _options.ClampLocation, recenter: false);
        }

        public void OnClosed(object? sender, EventArgs e)
        {
            _window.SourceInitialized -= OnSourceInitialized;
            _window.Loaded -= OnLoaded;
            _window.SizeChanged -= OnSizeChanged;
            _window.LocationChanged -= OnLocationChanged;
            _window.Closed -= OnClosed;
        }

        private void ApplyBounds(bool clampLocation, bool recenter)
        {
            if (_isApplying || _window.WindowState == WindowState.Maximized)
            {
                return;
            }

            if (!TryGetWorkArea(_window, out var workArea))
            {
                return;
            }

            _isApplying = true;
            try
            {
                var maxWidth = Math.Floor(workArea.Width * _options.MaxWidthRatio);
                var maxHeight = Math.Floor(workArea.Height * _options.MaxHeightRatio);

                if (_window.MinWidth > maxWidth)
                {
                    _window.MinWidth = maxWidth;
                }

                if (_window.MinHeight > maxHeight)
                {
                    _window.MinHeight = maxHeight;
                }

                _window.MaxWidth = maxWidth;
                _window.MaxHeight = maxHeight;

                if (!double.IsNaN(_window.Width) && _window.Width > maxWidth)
                {
                    _window.Width = maxWidth;
                }

                if (!double.IsNaN(_window.Height) && _window.Height > maxHeight)
                {
                    _window.Height = maxHeight;
                }

                if (!clampLocation && !recenter)
                {
                    return;
                }

                var actualWidth = ResolveWindowWidth(_window, maxWidth);
                var actualHeight = ResolveWindowHeight(_window, maxHeight);

                if (recenter)
                {
                    _window.Left = workArea.Left + Math.Max(0, (workArea.Width - actualWidth) / 2);
                    _window.Top = workArea.Top + Math.Max(0, (workArea.Height - actualHeight) / 2);
                    return;
                }

                var minLeft = workArea.Left;
                var maxLeft = Math.Max(workArea.Left, workArea.Right - actualWidth);
                var minTop = workArea.Top;
                var maxTop = Math.Max(workArea.Top, workArea.Bottom - actualHeight);

                _window.Left = Math.Clamp(_window.Left, minLeft, maxLeft);
                _window.Top = Math.Clamp(_window.Top, minTop, maxTop);
            }
            finally
            {
                _isApplying = false;
            }
        }
    }

    private static double ResolveWindowWidth(Window window, double fallback)
    {
        if (window.ActualWidth > 0)
        {
            return Math.Min(window.ActualWidth, fallback);
        }

        if (!double.IsNaN(window.Width) && window.Width > 0)
        {
            return Math.Min(window.Width, fallback);
        }

        return fallback;
    }

    private static double ResolveWindowHeight(Window window, double fallback)
    {
        if (window.ActualHeight > 0)
        {
            return Math.Min(window.ActualHeight, fallback);
        }

        if (!double.IsNaN(window.Height) && window.Height > 0)
        {
            return Math.Min(window.Height, fallback);
        }

        return fallback;
    }

    private static bool TryGetWorkArea(Window window, out Rect workArea)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            workArea = new Rect(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
            return true;
        }

        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            workArea = new Rect(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
            return true;
        }

        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            workArea = new Rect(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
            return true;
        }

        workArea = new Rect(
            info.rcWork.Left,
            info.rcWork.Top,
            info.rcWork.Right - info.rcWork.Left,
            info.rcWork.Bottom - info.rcWork.Top);
        return true;
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public RectNative rcMonitor;
        public RectNative rcWork;
        public uint dwFlags;
    }
}
