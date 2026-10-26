using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FastDbExplorer.Domain;
using FastDbExplorer.Wpf.Localization;

namespace FastDbExplorer.Wpf.ViewModels;

/// <summary>One row of the monitor list: the definition plus what the screen shows about its last check.</summary>
public sealed partial class MonitorItemViewModel : ObservableObject
{
    public MonitorItemViewModel(MonitorDefinition definition)
    {
        _definition = definition;
        _statusText = definition.LastCheckedUtc is null ? MonitoringStrings.FirstCheckNote : "";
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name), nameof(Target), nameof(IntervalText), nameof(LastCheckedText), nameof(IsEnabled))]
    private MonitorDefinition _definition;

    [ObservableProperty] private string _statusText;
    [ObservableProperty] private bool _hasProblem;
    [ObservableProperty] private bool _isChecking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotConfirmingDelete))]
    private bool _isConfirmingDelete;

    /// <summary>Failed checks in a row (memory only). Used to alert on the first failure without repeating every interval.</summary>
    public int ConsecutiveFailures { get; set; }

    public string Name => Definition.Name;
    public string Target => Definition.TargetText;
    public string IntervalText => MonitoringStrings.EveryMinutes(Definition.IntervalMinutes);
    public bool IsEnabled => Definition.Enabled;
    public bool IsNotConfirmingDelete => !IsConfirmingDelete;

    public string LastCheckedText =>
        Definition.LastCheckedUtc is { } checkedAt ? MonitoringStrings.LastChecked(checkedAt) : MonitoringStrings.NeverChecked;

    [RelayCommand]
    private void RequestDelete() => IsConfirmingDelete = true;

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;
}
