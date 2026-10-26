using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FastDbExplorer.Wpf.Localization;

namespace FastDbExplorer.Wpf.ViewModels;

/// <summary>
/// The map can also live in its own window (for example on a second monitor). The view model only knows the state and
/// raises the requests; the views create and close the window, so this class never touches UI controls.
/// </summary>
public sealed partial class MapViewModel
{
    /// <summary>The user asked to open the map in its own window. The Map section's view creates it.</summary>
    public event Action? PopOutRequested;

    /// <summary>The user asked to bring the detached map back. The detached window closes itself.</summary>
    public event Action? DockRequested;

    /// <summary>True while the map is shown in its own window; the Map section then shows a short note instead of the map.</summary>
    [ObservableProperty] private bool _isPoppedOut;

    public string PopOutText => IsPoppedOut ? MapUiStrings.DockBack : MapUiStrings.PopOut;

    partial void OnIsPoppedOutChanged(bool value) => OnPropertyChanged(nameof(PopOutText));

    /// <summary>One button both ways: open the map in a window, or bring it back when it is already in one.</summary>
    [RelayCommand]
    private void ToggleWindow()
    {
        if (IsPoppedOut) DockRequested?.Invoke();
        else PopOutRequested?.Invoke();
    }

    /// <summary>Called by the detached window when it opens (true) and when it closes (false).</summary>
    public void SetPoppedOut(bool value) => IsPoppedOut = value;
}
