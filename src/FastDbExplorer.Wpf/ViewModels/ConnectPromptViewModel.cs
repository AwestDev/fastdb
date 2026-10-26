using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FastDbExplorer.Wpf.Localization;

namespace FastDbExplorer.Wpf.ViewModels;

/// <summary>Shown instead of Explorer/Monitoring while there is no connection: an invitation, not an empty page.</summary>
public sealed partial class ConnectPromptViewModel : ObservableObject
{
    private readonly Action _goToConnect;

    public ConnectPromptViewModel(string title, string body, string glyph, Action goToConnect)
    {
        Title = title;
        Body = body;
        Glyph = glyph;
        _goToConnect = goToConnect;
    }

    public string Title { get; }
    public string Body { get; }
    public string Glyph { get; }
    public string ButtonText => ShellStrings.GoToConnect;

    [RelayCommand]
    private void GoToConnect() => _goToConnect();
}
