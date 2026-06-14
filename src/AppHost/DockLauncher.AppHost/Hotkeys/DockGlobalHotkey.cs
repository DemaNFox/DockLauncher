using System.Runtime.InteropServices;
using System.Windows.Interop;
using DockLauncher.BuildingBlocks.Application.Contracts;
using DockLauncher.Modules.Settings.Application;
using Microsoft.Extensions.DependencyInjection;

namespace DockLauncher.AppHost.Hotkeys;

public sealed class DockGlobalHotkey : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const int HotkeyId = 0x444F434B;

    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkspaceStore _workspaceStore;
    private HwndSource? _source;
    private string? _registeredHotkey;

    public DockGlobalHotkey(IServiceProvider serviceProvider, IWorkspaceStore workspaceStore)
    {
        _serviceProvider = serviceProvider;
        _workspaceStore = workspaceStore;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_source is null)
        {
            var parameters = new HwndSourceParameters("DockLauncherHotkeySink")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0
            };

            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);
        }

        await RefreshRegistrationAsync(cancellationToken);
    }

    public async Task RefreshRegistrationAsync(CancellationToken cancellationToken = default)
    {
        if (_source is null)
        {
            return;
        }

        var workspace = await _workspaceStore.LoadAsync(cancellationToken);
        var hotkey = workspace.Settings.GlobalHotkey?.Trim();
        if (string.Equals(_registeredHotkey, hotkey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        UnregisterCurrentHotkey();
        if (TryParseHotkey(hotkey, out var modifiers, out var virtualKey))
            RegisterHotKey(_source.Handle, HotkeyId, modifiers, virtualKey);

        _registeredHotkey = hotkey;
    }

    public void Dispose()
    {
        UnregisterCurrentHotkey();
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }

    private void UnregisterCurrentHotkey()
    {
        if (_source is not null)
            UnregisterHotKey(_source.Handle, HotkeyId);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && wParam.ToInt32() == HotkeyId)
        {
            _serviceProvider.GetRequiredService<IDockShellController>().TogglePanelsVisibility();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public static bool TryParseHotkey(string? hotkey, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return false;
        }

        var tokens = hotkey
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
        {
            return false;
        }

        foreach (var token in tokens[..^1])
        {
            switch (token.ToUpperInvariant())
            {
                case "ALT":
                    modifiers |= 0x0001;
                    break;
                case "CTRL":
                case "CONTROL":
                    modifiers |= 0x0002;
                    break;
                case "SHIFT":
                    modifiers |= 0x0004;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= 0x0008;
                    break;
                default:
                    return false;
            }
        }

        return TryParseVirtualKey(tokens[^1], out virtualKey);
    }

    private static bool TryParseVirtualKey(string token, out uint virtualKey)
    {
        virtualKey = 0;
        var normalized = token.Trim().ToUpperInvariant();

        if (normalized.Length == 1)
        {
            var character = normalized[0];
            if (character is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                virtualKey = character;
                return true;
            }
        }

        if (normalized == "SPACE")
        {
            virtualKey = 0x20;
            return true;
        }

        if (normalized is "ESC" or "ESCAPE")
        {
            virtualKey = 0x1B;
            return true;
        }

        if (normalized is "ENTER" or "RETURN")
        {
            virtualKey = 0x0D;
            return true;
        }

        if (normalized == "TAB")
        {
            virtualKey = 0x09;
            return true;
        }

        if (normalized.StartsWith('F')
            && int.TryParse(normalized[1..], out var functionKey)
            && functionKey is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + functionKey - 1);
            return true;
        }

        return false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
