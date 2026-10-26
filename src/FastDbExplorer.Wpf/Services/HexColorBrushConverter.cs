using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Wpf.Services;

/// <summary>"#RRGGBB" text to a brush for colour swatches. Text that is not a valid colour gives a transparent brush.</summary>
public sealed class HexColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string hex && MapMarkerCatalog.IsValidColor(hex)
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex))
            : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
