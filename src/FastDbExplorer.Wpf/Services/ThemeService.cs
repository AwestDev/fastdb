using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using WpfApp = System.Windows.Application;

namespace FastDbExplorer.Wpf.Services;

/// <summary>Follows the Windows light/dark setting and swaps the colour dictionary live.</summary>
public sealed class ThemeService : IDisposable
{
    public void Start()
    {
        Apply();
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;

    public static bool IsDark()
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
        if (e.Category == UserPreferenceCategory.General)
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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
