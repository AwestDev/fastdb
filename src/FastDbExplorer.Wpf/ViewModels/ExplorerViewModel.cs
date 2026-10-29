using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FastDbExplorer.Application.Abstractions;
using FastDbExplorer.Domain;
using FastDbExplorer.Infrastructure;
using FastDbExplorer.Wpf.Localization;
using FastDbExplorer.Wpf.Services;

namespace FastDbExplorer.Wpf.ViewModels;

public sealed partial class ExplorerViewModel : ObservableObject
{
    private readonly IDatabaseMetadataService _metadata;
    private readonly IFileDialogService _dialogs;
    private readonly IQueryReportWriter _queryReportWriter;
    private readonly ConnectionSettings _settings;
    private readonly ObservableCollection<TableInfo> _tables = [];
    private CancellationTokenSource? _loadCts;

    public ExplorerViewModel(
        IDatabaseMetadataService metadata, ConnectionSettings settings, IReadOnlyList<DatabaseInfo> databases,
        IFileDialogService dialogs, IQueryReportWriter queryReportWriter)
    {
        _metadata = metadata;
        _settings = settings;
        _dialogs = dialogs;
        _queryReportWriter = queryReportWriter;
        Databases = new ObservableCollection<DatabaseInfo>(databases);
        Tables = CollectionViewSource.GetDefaultView(_tables);
        Tables.Filter = MatchesFilter;
        SelectedDatabase = Databases.FirstOrDefault();
    }

    // ---- nearby search point: entered by hand or handed over from the map --------------------------------------

    [ObservableProperty] private string _searchLatitudeText = "";
    [ObservableProperty] private string _searchLongitudeText = "";
    [ObservableProperty] private string _searchRadiusText = "1000";
    [ObservableProperty] private string _searchError = "";
    [ObservableProperty] private string _searchPointText = "";
    private SearchPoint? _searchPoint;

    /// <summary>A point chosen on the map. Keeps the radius the user already set.</summary>
    public void ApplySearchPoint(double latitude, double longitude)
    {
        if (!TryParseRadius(out var radius)) return;
        SetSearchPoint(new SearchPoint(latitude, longitude, radius));
    }

    [RelayCommand]
    private void ApplyManualPoint()
    {
        if (!TryParseCoordinates(out var latitude, out var longitude) || !TryParseRadius(out var radius)) return;
        SetSearchPoint(new SearchPoint(latitude, longitude, radius));
    }

    [RelayCommand]
    private void ClearSearchPoint()
    {
        SearchLatitudeText = "";
        SearchLongitudeText = "";
        SearchError = "";
        SetSearchPoint(null);
    }

    private void SetSearchPoint(SearchPoint? point)
    {
        _searchPoint = point;
        if (point is null)
        {
            SearchPointText = "";
        }
        else
        {
            SearchLatitudeText = FormattableString.Invariant($"{point.Latitude:F6}");
            SearchLongitudeText = FormattableString.Invariant($"{point.Longitude:F6}");
            SearchRadiusText = FormattableString.Invariant($"{point.RadiusMeters:F0}");
            SearchPointText = FormattableString.Invariant($"نتایج نزدیک {point.Latitude:F6}, {point.Longitude:F6} در شعاع {point.RadiusMeters:F0} متر");
        }
        Query?.SetSearchPoint(point);
    }

    private bool TryParseCoordinates(out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;
        if (!double.TryParse(SearchLatitudeText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
            || latitude < -90 || latitude > 90)
        {
            SearchError = "عرض جغرافیایی باید عددی بین -90 و 90 باشد.";
            return false;
        }
        if (!double.TryParse(SearchLongitudeText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out longitude)
            || longitude < -180 || longitude > 180)
        {
            SearchError = "طول جغرافیایی باید عددی بین -180 و 180 باشد.";
            return false;
        }
        SearchError = "";
        return true;
    }

    private bool TryParseRadius(out double radius)
    {
        if (!double.TryParse(SearchRadiusText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out radius)
            || radius <= 0 || radius > SpatialSearch.MaxRadiusMeters)
        {
            SearchError = "شعاع باید عددی بین ۱ و ۵۰۰٬۰۰۰ متر باشد.";
            return false;
        }
        SearchError = "";
        return true;
    }

    public event Action? Disconnected;
    public event Action? OpenMapRequested;
    public event Action? OpenMonitoringRequested;
    public event Action<string, IReadOnlyList<QueryMapPoint>>? ShowSelectedRowsOnMapRequested;

    public string ServerName => _settings.Server;
    public ObservableCollection<DatabaseInfo> Databases { get; }
    public ICollectionView Tables { get; }

    [ObservableProperty] private DatabaseInfo? _selectedDatabase;
    [ObservableProperty] private TableInfo? _selectedTable;
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private string _tablesStatus = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private TableQueryViewModel? _query;

    partial void OnSelectedDatabaseChanged(DatabaseInfo? value) => _ = LoadTablesAsync(value);

    partial void OnSelectedTableChanged(TableInfo? value)
    {
        Query?.Shutdown(); // cancels any running query of the previous table
        if (value is null || SelectedDatabase is null) { Query = null; return; }
        var query = new TableQueryViewModel(_metadata, _settings, SelectedDatabase.Name, value, _dialogs, _queryReportWriter);
        query.ShowSelectedRowsOnMapRequested += (name, points) => ShowSelectedRowsOnMapRequested?.Invoke(name, points);
        query.SetSearchPoint(_searchPoint); // a point already chosen applies to the new table too
        Query = query;
        _ = query.InitializeAsync();
    }

    partial void OnFilterTextChanged(string value)
    {
        Tables.Refresh();
        UpdateStatus();
    }

    [RelayCommand]
    private void OpenMap() => OpenMapRequested?.Invoke();

    [RelayCommand]
    private void OpenMonitoring() => OpenMonitoringRequested?.Invoke();

    [RelayCommand]
    private Task ReloadAsync() => LoadTablesAsync(SelectedDatabase);

    [RelayCommand]
    private void Disconnect()
    {
        Shutdown();
        Disconnected?.Invoke();
    }

    public void Shutdown()
    {
        _loadCts?.Cancel();
        Query?.Shutdown();
    }

    private bool MatchesFilter(object item) =>
        string.IsNullOrWhiteSpace(FilterText)
        || (item is TableInfo t && t.FullName.Contains(FilterText.Trim(), StringComparison.OrdinalIgnoreCase));

    private async Task LoadTablesAsync(DatabaseInfo? database)
    {
        _loadCts?.Cancel(); // a newer selection supersedes any load still running
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        _tables.Clear();
        SelectedTable = null;
        ErrorMessage = "";
        if (database is null) { UpdateStatus(); return; }

        IsLoading = true;
        UpdateStatus();
        try
        {
            var list = await _metadata.GetTablesAsync(_settings, database.Name, cts.Token);
            if (cts.IsCancellationRequested) return;
            foreach (var table in list) _tables.Add(table);
        }
        catch (OperationCanceledException) { return; }
        catch (DatabaseAccessException ex) { ErrorMessage = Strings.Describe(ex); }
        catch (Exception ex) { ErrorMessage = ex.Message; } // UI boundary: show it instead of crashing
        finally
        {
            if (_loadCts == cts)
            {
                IsLoading = false;
                UpdateStatus();
            }
        }
    }

    private void UpdateStatus()
    {
        if (IsLoading) { TablesStatus = Strings.Loading; return; }
        if (_tables.Count == 0) { TablesStatus = ErrorMessage.Length > 0 ? "" : Strings.NoTables; return; }
        TablesStatus = Strings.TablesCount(Tables.Cast<object>().Count(), _tables.Count);
    }
}
