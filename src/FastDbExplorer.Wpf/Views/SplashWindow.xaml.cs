using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Animation;

namespace FastDbExplorer.Wpf.Views;

/// <summary>Start-up splash. <see cref="App"/> shows it first and reports the real start-up steps through <see cref="Report"/>.</summary>
public partial class SplashWindow : Window
{
    private const double TrackWidth = 260;

    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = VersionLabel();
    }

    /// <summary>Shows the step text and calculates percentage from step/total.</summary>
    public void Report(int step, int total, string text)
    {
        StatusText.Text = text;
        var percentage = (int)((double)step / Math.Max(total, 1) * 100);
        PercentageText.Text = string.Format(CultureInfo.InvariantCulture, "{0}%", percentage);

        var fraction = Math.Clamp((double)step / Math.Max(total, 1), 0, 1);
        var animation = new DoubleAnimation(TrackWidth * fraction, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        ProgressFill.BeginAnimation(WidthProperty, animation);
    }

    private static string VersionLabel()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "" : string.Format(CultureInfo.InvariantCulture, "v{0}.{1}.{2}", version.Major, version.Minor, Math.Max(version.Build, 0));
    }
}
