using CommunityToolkit.Mvvm.ComponentModel;
using FastDbExplorer.Wpf.Localization;

namespace FastDbExplorer.Wpf.ViewModels;

/// <summary>The sections of the app, in navigation-rail order (Ctrl+1 .. Ctrl+5).</summary>
public enum AppSection { Home, Connections, Explorer, Map, Monitoring }

/// <summary>One button of the navigation rail.</summary>
public sealed partial class NavItemViewModel : ObservableObject
{
    public NavItemViewModel(AppSection section, string title, string glyph, int shortcutNumber)
    {
        Section = section;
        Title = title;
        Glyph = glyph;
        ToolTipText = ShellStrings.WithShortcut(title, shortcutNumber);
    }

    public AppSection Section { get; }
    public string Title { get; }
    public string Glyph { get; }
    public string ToolTipText { get; }

    [ObservableProperty] private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadge))]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    private int _badge;

    public bool HasBadge => Badge > 0;
    public string BadgeText => Badge > 99 ? "99+" : Badge.ToString();
}
