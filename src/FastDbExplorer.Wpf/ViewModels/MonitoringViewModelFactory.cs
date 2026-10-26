using FastDbExplorer.Application.Abstractions;
using FastDbExplorer.Domain;
using FastDbExplorer.Wpf.Services;

namespace FastDbExplorer.Wpf.ViewModels;

/// <summary>Creates the monitoring screen for a freshly connected session (the connection is only known at run time).</summary>
public sealed class MonitoringViewModelFactory(
    IDatabaseMetadataService metadata, IMonitorStore store, IMonitorCheckService checks,
    IFolderPickerService folders, IAlertNotifier notifier, ICoordinateLayerStore coordinateLayers)
{
    public MonitoringViewModel Create(ConnectionSettings settings, IReadOnlyList<DatabaseInfo> databases) =>
        new(metadata, store, checks, folders, notifier, coordinateLayers, settings, databases);
}
