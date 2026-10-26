using CommunityToolkit.Mvvm.ComponentModel;

namespace FastDbExplorer.Wpf.ViewModels;

/// <summary>Selection count of the result preview, shown by the shared "show on the map" panel (the Explorer already has its own).</summary>
public sealed partial class MonitoringViewModel
{
    [ObservableProperty] private int _selectedResultRowCount;

    public void UpdateSelectedResultRowCount(int count) => SelectedResultRowCount = count;
}
