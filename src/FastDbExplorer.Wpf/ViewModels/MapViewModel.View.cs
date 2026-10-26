namespace FastDbExplorer.Wpf.ViewModels;

/// <summary>
/// Remembers the map camera while the user works in another section. The map view is rebuilt every time its section is
/// opened, so without this the map would jump back to Iran each time. The page reports its camera after every move.
/// </summary>
public sealed partial class MapViewModel
{
    /// <summary>Longitude, latitude and zoom the user left the map at; null until the user has moved the map.</summary>
    public (double Lng, double Lat, double Zoom)? LastView { get; private set; }

    public void RememberView(double lng, double lat, double zoom)
    {
        if (double.IsFinite(lng) && double.IsFinite(lat) && double.IsFinite(zoom)) LastView = (lng, lat, zoom);
    }

    /// <summary>A different map file opens on Iran again (the saved camera belonged to the previous file).</summary>
    partial void OnFileNameChanged(string value) => LastView = null;
}
