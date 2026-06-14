using System.Windows;
using DockLauncher.AppHost.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DockLauncher.AppHost.Dialogs;

public partial class TextPromptWindow : Window
{
    public TextPromptWindow(string title, string message, string initialValue)
    {
        InitializeComponent();
        WindowDisplayPolicy.Apply(this, new WindowDisplayPolicyOptions(RecenterOnLoad: true));
        var viewModel = new TextPromptWindowViewModel(title, message, initialValue, Accept);
        DataContext = viewModel;
    }

    public string ResponseText => DataContext is TextPromptWindowViewModel viewModel ? viewModel.ResponseText : string.Empty;

    private void Accept()
    {
        DialogResult = true;
        Close();
    }
}

public sealed partial class TextPromptWindowViewModel : ObservableObject
{
    private readonly Action _accept;

    public TextPromptWindowViewModel(string windowTitle, string promptMessage, string responseText, Action accept)
    {
        WindowTitle = windowTitle;
        PromptMessage = promptMessage;
        this.responseText = responseText;
        _accept = accept;
    }

    public string WindowTitle { get; }

    public string PromptMessage { get; }

    [ObservableProperty]
    private string responseText;

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void Accept()
    {
        _accept();
    }

    private bool CanAccept()
    {
        return !string.IsNullOrWhiteSpace(ResponseText);
    }
}
