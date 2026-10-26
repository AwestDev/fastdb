using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FastDbExplorer.Wpf.Localization;

namespace FastDbExplorer.Wpf.ViewModels;

/// <summary>Home dashboard of a connected session: connection summary, shortcuts to the sections, latest alerts.</summary>
public sealed partial class HomeViewModel : ObservableObject, IDisposable
{
    private const int MaxRecentAlerts = 5;

    private readonly MonitoringViewModel _monitoring;
    private readonly Action<AppSection> _navigate;
    private readonly Action _disconnect;

    public HomeViewModel(
        string serverName, int databaseCount, MonitoringViewModel monitoring, Action<AppSection> navigate, Action disconnect)
    {
        ServerName = serverName;
        DatabasesText = ShellStrings.DatabasesCount(databaseCount);
        _monitoring = monitoring;
        _navigate = navigate;
        _disconnect = disconnect;

        RecentAlerts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoAlerts));
        _monitoring.Monitors.CollectionChanged += OnMonitorsChanged;
        _monitoring.Alerts.CollectionChanged += OnAlertsChanged;
        UpdateMonitors();
        UpdateAlerts();
    }

    public string ServerName { get; }
    public string DatabasesText { get; }
    public ObservableCollection<AlertItem> RecentAlerts { get; } = [];
    public bool HasNoAlerts => RecentAlerts.Count == 0;

    [ObservableProperty] private string _monitorsText = "";

    [RelayCommand]
    private void OpenExplorer() => _navigate(AppSection.Explorer);

    [RelayCommand]
    private void OpenMap() => _navigate(AppSection.Map);

    [RelayCommand]
    private void OpenMonitoring() => _navigate(AppSection.Monitoring);

    [RelayCommand]
    private void Disconnect() => _disconnect();

    private void OnMonitorsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateMonitors();

    private void OnAlertsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateAlerts();

    private void UpdateMonitors() => MonitorsText = ShellStrings.MonitorsCount(_monitoring.Monitors.Count);

    private void UpdateAlerts()
    {
        RecentAlerts.Clear();
        foreach (var alert in _monitoring.Alerts.Take(MaxRecentAlerts)) RecentAlerts.Add(alert);
    }

    public void Dispose()
    {
        _monitoring.Monitors.CollectionChanged -= OnMonitorsChanged;
        _monitoring.Alerts.CollectionChanged -= OnAlertsChanged;
    }
}
