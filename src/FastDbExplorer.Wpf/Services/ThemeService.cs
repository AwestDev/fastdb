using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using WpfApp = System.Windows.Application;

namespace FastDbExplorer.Wpf.Services;

/// <summary>The colour theme the user chose. <see cref="System"/> follows the Windows light/dark setting.</summary>
public enum ThemeMode { Dark, Light, System }

/// <summary>Applies the chosen colour theme, remembers it, and swaps the colour dictionary live.</summary>
public sealed class ThemeService : IDisposable
{
    private static ThemeMode _mode = LoadMode();

    /// <summary>Fired after the user changes the theme.</summary>
    public static event Action? ModeChanged;

    public static ThemeMode Mode => _mode;

    public static void SetMode(ThemeMode mode)
    {
        if (mode == _mode) return;
        _mode = mode;
        SaveMode(mode);
        if (WpfApp.Current is not null) Apply();
        ModeChanged?.Invoke();
    }

    public void Start()
    {
        Apply();
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;

    /// <summary>True when the dark palette is in effect (the chosen mode, or the Windows setting for System).</summary>
    public static bool IsDark() => _mode switch
    {
        ThemeMode.Dark => true,
        ThemeMode.Light => false,
        _ => WindowsPrefersDark(),
    };

    public static bool WindowsPrefersDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    public static void SetDarkTitleBar(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var value = dark ? 1 : 0;
        DwmSetWindowAttribute(handle, 20, ref value, sizeof(int)); // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE
    }

    private void OnPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _mode == ThemeMode.System)
            WpfApp.Current.Dispatcher.Invoke(Apply);
    }

    private static void Apply()
    {
        var dark = IsDark();
        // Index 0 of the merged dictionaries is always the colour theme (see App.xaml).
        WpfApp.Current.Resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        };
        foreach (Window window in WpfApp.Current.Windows)
            SetDarkTitleBar(window, dark);
    }

    private static string SettingsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FastDbExplorer", "settings", "theme.json");

    private sealed record ThemeFile(string? Mode);

    private static ThemeMode LoadMode()
    {
        try
        {
            if (File.Exists(SettingsFile)
                && JsonSerializer.Deserialize<ThemeFile>(File.ReadAllText(SettingsFile)) is { Mode: { } saved }
                && Enum.TryParse<ThemeMode>(saved, out var mode))
                return mode;
        }
        catch (Exception)
        {
            // A damaged settings file must not stop the app; the default theme is used.
        }
        return ThemeMode.Dark;
    }

    private static void SaveMode(ThemeMode mode)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(new ThemeFile(mode.ToString())));
        }
        catch (Exception)
        {
            // Saving the choice is a convenience; the theme still applies for this session.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
