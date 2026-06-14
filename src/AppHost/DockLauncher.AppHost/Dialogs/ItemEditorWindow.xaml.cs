using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media.Imaging;
using DockLauncher.AppHost.Configuration;
using Microsoft.Win32;

namespace DockLauncher.AppHost.Dialogs;

public partial class ItemEditorWindow : Window
{
    public ItemEditorWindow(ItemEditorRequest request, IRemoteIconCache remoteIconCache)
    {
        InitializeComponent();
        WindowDisplayPolicy.Apply(this, new WindowDisplayPolicyOptions(RecenterOnLoad: true));
        DataContext = new ItemEditorWindowViewModel(request, remoteIconCache, Accept);
    }

    public ItemEditorResult? Result => DataContext is ItemEditorWindowViewModel viewModel
        ? viewModel.Result
        : null;

    private void Accept()
    {
        DialogResult = true;
        Close();
    }
}

public sealed partial class ItemEditorWindowViewModel : ObservableObject
{
    private readonly IRemoteIconCache _remoteIconCache;
    private readonly Action _accept;

    public ItemEditorWindowViewModel(ItemEditorRequest request, IRemoteIconCache remoteIconCache, Action accept)
    {
        _remoteIconCache = remoteIconCache;
        _accept = accept;
        WindowTitle = $"Edit {request.DisplayName}";
        HelperText = request.HelperText;
        DisplayName = request.DisplayName;
        TypeLabel = request.Type.ToString();
        Target = request.Target;
        Arguments = request.Arguments;
        RunAsAdministrator = request.RunAsAdministrator;
        IconPath = request.IconPath;
        IsTargetReadOnly = !request.TargetEditable;
    }

    public string WindowTitle { get; }

    public string HelperText { get; }

    public string TypeLabel { get; }

    public bool IsTargetReadOnly { get; }

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string target = string.Empty;

    [ObservableProperty]
    private string? arguments;

    [ObservableProperty]
    private bool runAsAdministrator;

    [ObservableProperty]
    private string? iconPath;

    [ObservableProperty]
    private string? validationMessage;

    public ItemEditorResult? Result { get; private set; }

    [RelayCommand]
    private void BrowseIcon()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select custom icon",
            Filter = "Images and icons|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe;*.lnk|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            IconPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void ClearIcon()
    {
        IconPath = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidationMessage = null;
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            ValidationMessage = "Display name is required.";
            return;
        }

        if (!IsTargetReadOnly && string.IsNullOrWhiteSpace(Target))
        {
            ValidationMessage = "Target is required.";
            return;
        }

        var resolvedIconPath = string.IsNullOrWhiteSpace(IconPath) ? null : IconPath.Trim();
        if (_remoteIconCache.IsRemoteIconUrl(resolvedIconPath))
        {
            try
            {
                resolvedIconPath = await _remoteIconCache.CacheAsync(resolvedIconPath!);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or NotSupportedException or FileFormatException or IOException)
            {
                ValidationMessage = $"Remote icon could not be loaded: {ex.Message}";
                return;
            }
        }

        Result = new ItemEditorResult(
            DisplayName.Trim(),
            IsTargetReadOnly ? Target : Target.Trim(),
            string.IsNullOrWhiteSpace(Arguments) ? null : Arguments.Trim(),
            RunAsAdministrator,
            resolvedIconPath);

        _accept();
    }
}
