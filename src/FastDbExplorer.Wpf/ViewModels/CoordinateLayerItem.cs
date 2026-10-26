using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FastDbExplorer.Domain;
using FastDbExplorer.Wpf.Localization;
using FastDbExplorer.Wpf.Services;

namespace FastDbExplorer.Wpf.ViewModels;

/// <summary>
/// One imported Excel layer in the panel: name, point count, radius (metres), visibility, marker icon/colour,
/// and the zoom / refresh / rename / delete commands.
/// </summary>
public sealed partial class CoordinateLayerItem : ObservableObject
{
    private readonly MapViewModel _owner;
    private bool _syncing; // true while values are copied from the model: those changes must not be saved again

    public CoordinateLayerItem(MapLayer layer, MapViewModel owner)
    {
        _owner = owner;
        Layer = layer;
        IconCategories = LayerStyleChoices.BuildIconCategories(SetIcon, includeDefault: true);
        IconCategories[0].IsSelected = true;
        SelectedIconCategory = IconCategories[0];
        ColorChoices = LayerStyleChoices.BuildColorChoices(SetColor);
        _syncing = true;
        try
        {
            IsVisible = layer.Visibility;
            RadiusText = FormatRadius(layer.RadiusMeters);
        }
        finally
        {
            _syncing = false;
        }
        RefreshStyleState();
    }

    public MapLayer Layer { get; private set; }

    public Guid Id => Layer.Id;

    public string Name => Layer.Name;

    public string FileName => Layer.FileName;

    public string Summary => CoordinateStrings.Summary(Layer.Points.Count, Layer.FileName);

    /// <summary>"1,250 نقطه"</summary>
    public string PointsText => CoordinateStrings.PointsChip(Layer.Points.Count);

    /// <summary>"شعاع 500 متر" or "بدون شعاع" (the applied radius, not the text being typed).</summary>
    public string RadiusSummary => CoordinateStrings.RadiusChip(Layer.RadiusMeters);

    /// <summary>One line under the name in the collapsed card.</summary>
    public string Subtitle => PointsText + " · " + RadiusSummary;

    // ---- marker style (icon + colour) -------------------------------------------------------------------------

    /// <summary>The icon picker's folders, in catalog order. The first folder also holds the default dot marker.</summary>
    public IReadOnlyList<IconCategoryChoice> IconCategories { get; }

    public IReadOnlyList<ColorChoice> ColorChoices { get; }

    public Geometry? IconShape => MapIconGeometry.For(Layer.IconKey);
    public bool HasIcon => IconShape is not null;
    public bool HasNoIcon => !HasIcon;

    /// <summary>The glyph shown on the picker button: the chosen icon, or the default dot.</summary>
    public Geometry? DisplayIconShape => IconShape ?? MapIconGeometry.For("dot");

    /// <summary>The name shown on the picker button.</summary>
    public string CurrentIconName => HasIcon ? MapUiStrings.IconName(Layer.IconKey!) : MapUiStrings.DefaultMarker;

    public Brush ColorBrush => MapIconGeometry.BrushFor(MapMarkerCatalog.EffectiveColor(Layer.Id, Layer.Color));

    [ObservableProperty] private IconCategoryChoice? _selectedIconCategory;
    [ObservableProperty] private bool _isIconPickerOpen;

    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private string _radiusText = "";
    [ObservableProperty] private string _radiusError = "";

    [ObservableProperty] private bool _isRenaming;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editError = "";
    [ObservableProperty] private bool _isConfirmingDelete;

    public bool HasRadiusError => RadiusError.Length > 0;
    public bool HasEditError => EditError.Length > 0;
    public bool IsNotRenaming => !IsRenaming;
    public bool IsNotConfirmingDelete => !IsConfirmingDelete;

    partial void OnRadiusErrorChanged(string value) => OnPropertyChanged(nameof(HasRadiusError));
    partial void OnEditErrorChanged(string value) => OnPropertyChanged(nameof(HasEditError));
    partial void OnIsRenamingChanged(bool value) => OnPropertyChanged(nameof(IsNotRenaming));
    partial void OnIsConfirmingDeleteChanged(bool value) => OnPropertyChanged(nameof(IsNotConfirmingDelete));

    partial void OnIsVisibleChanged(bool value)
    {
        if (!_syncing) _owner.OnCoordinateLayerVisibilityChanged(this, value);
    }

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private void ToggleIconPicker() => IsIconPickerOpen = !IsIconPickerOpen;

    [RelayCommand]
    private void SelectIconCategory(IconCategoryChoice? category)
    {
        if (category is null) return;
        foreach (var item in IconCategories) item.IsSelected = ReferenceEquals(item, category);
        SelectedIconCategory = category;
    }

    [RelayCommand]
    private Task ApplyRadiusAsync() => _owner.ApplyRadiusAsync(this);

    [RelayCommand]
    private void Zoom() => _owner.ZoomToCoordinateLayer(this);

    [RelayCommand]
    private Task RefreshAsync() => _owner.RefreshCoordinateLayerAsync(this);

    [RelayCommand]
    private void BeginRename()
    {
        IsConfirmingDelete = false;
        EditName = Layer.Name;
        EditError = "";
        IsRenaming = true;
    }

    [RelayCommand]
    private Task CommitRenameAsync() => _owner.RenameCoordinateLayerAsync(this);

    [RelayCommand]
    private void CancelRename()
    {
        EditError = "";
        IsRenaming = false;
    }

    [RelayCommand]
    private void RequestDelete()
    {
        IsRenaming = false;
        IsConfirmingDelete = true;
    }

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        await _owner.RemoveCoordinateLayerAsync(this);
        IsConfirmingDelete = false; // stays visible only when the removal failed
    }

    /// <summary>Chosen in the picker: null = the default dot marker.</summary>
    internal void SetIcon(string? key)
    {
        IsIconPickerOpen = false;
        if (Layer.IconKey == key) return;
        Layer.IconKey = key;
        RefreshStyleState();
        _ = _owner.ApplyLayerStyleAsync(this);
    }

    internal void SetColor(string hex)
    {
        if (string.Equals(Layer.Color, hex, StringComparison.OrdinalIgnoreCase)) return;
        Layer.Color = hex;
        RefreshStyleState();
        _ = _owner.ApplyLayerStyleAsync(this);
    }

    private void RefreshStyleState()
    {
        var effective = MapMarkerCatalog.EffectiveColor(Layer.Id, Layer.Color);
        foreach (var category in IconCategories)
            foreach (var choice in category.Icons)
                choice.IsSelected = choice.Key == Layer.IconKey;
        foreach (var choice in ColorChoices) choice.IsSelected = string.Equals(choice.Hex, effective, StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(IconShape));
        OnPropertyChanged(nameof(HasIcon));
        OnPropertyChanged(nameof(HasNoIcon));
        OnPropertyChanged(nameof(DisplayIconShape));
        OnPropertyChanged(nameof(CurrentIconName));
        OnPropertyChanged(nameof(ColorBrush));
    }

    /// <summary>Takes over a freshly loaded copy of the layer without triggering a save.</summary>
    internal void Sync(MapLayer layer)
    {
        Layer = layer;
        _syncing = true;
        try
        {
            IsVisible = layer.Visibility;
            RadiusText = FormatRadius(layer.RadiusMeters);
            RadiusError = "";
        }
        finally
        {
            _syncing = false;
        }
        NotifyLayerDataChanged();
        RefreshStyleState();
    }

    /// <summary>Raises the change notifications for everything derived from the model (name, counts, radius chip).</summary>
    internal void NotifyLayerDataChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(PointsText));
        OnPropertyChanged(nameof(RadiusSummary));
        OnPropertyChanged(nameof(Subtitle));
    }

    internal static string FormatRadius(double meters) => meters.ToString("0.###", CultureInfo.InvariantCulture);
}
