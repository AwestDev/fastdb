using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FastDbExplorer.Domain;
using FastDbExplorer.Wpf.Localization;
using FastDbExplorer.Wpf.Services;

namespace FastDbExplorer.Wpf.ViewModels;

/// <summary>One selectable marker icon in a picker (Key null = the default dot marker of an Excel layer).</summary>
public sealed partial class IconChoice : ObservableObject
{
    private readonly Action<string?> _onSelect;

    public IconChoice(Action<string?> onSelect, string? key)
    {
        _onSelect = onSelect;
        Key = key;
        Name = key is null ? MapUiStrings.DefaultMarker : MapUiStrings.IconName(key);
        Shape = MapIconGeometry.For(key ?? "dot");
    }

    public string? Key { get; }
    public string Name { get; }
    public Geometry? Shape { get; }

    [ObservableProperty] private bool _isSelected;

    [RelayCommand]
    private void Select() => _onSelect(Key);
}

/// <summary>One folder of an icon picker (for example "health"): its name, its icons and whether it is the open one.</summary>
public sealed partial class IconCategoryChoice : ObservableObject
{
    public IconCategoryChoice(string key, string name, IReadOnlyList<IconChoice> icons)
    {
        Key = key;
        Name = name;
        Icons = icons;
    }

    public string Key { get; }
    public string Name { get; }
    public IReadOnlyList<IconChoice> Icons { get; }

    [ObservableProperty] private bool _isSelected;
}

/// <summary>One selectable marker colour in a picker.</summary>
public sealed partial class ColorChoice : ObservableObject
{
    private readonly Action<string> _onSelect;

    public ColorChoice(Action<string> onSelect, string hex)
    {
        _onSelect = onSelect;
        Hex = hex;
        Brush = MapIconGeometry.BrushFor(hex);
    }

    public string Hex { get; }
    public Brush Brush { get; }

    [ObservableProperty] private bool _isSelected;

    [RelayCommand]
    private void Select() => _onSelect(Hex);
}

/// <summary>Builds the icon folders and colour swatches shared by the Excel layer picker and the monitor picker.</summary>
public static class LayerStyleChoices
{
    public static List<IconCategoryChoice> BuildIconCategories(Action<string?> onSelect, bool includeDefault)
    {
        var categories = new List<IconCategoryChoice>();
        foreach (var category in MapMarkerCatalog.Categories)
        {
            var icons = category.Icons.Select(icon => new IconChoice(onSelect, icon.Key)).ToList();
            if (includeDefault && category.Key == MapMarkerCatalog.DefaultCategory) icons.Insert(0, new IconChoice(onSelect, null));
            categories.Add(new IconCategoryChoice(category.Key, MapUiStrings.CategoryName(category.Key), icons));
        }
        return categories;
    }

    public static List<ColorChoice> BuildColorChoices(Action<string> onSelect) =>
        [.. MapMarkerCatalog.Palette.Select(hex => new ColorChoice(onSelect, hex))];
}
