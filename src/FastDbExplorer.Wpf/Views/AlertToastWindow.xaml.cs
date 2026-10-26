using System.Windows;
using System.Windows.Threading;
using FastDbExplorer.Wpf.Services;

namespace FastDbExplorer.Wpf.Views;

/// <summary>Small always-on-top message in the bottom corner. It never takes keyboard focus and closes itself.</summary>
public partial class AlertToastWindow : Window
{
    private static readonly List<AlertToastWindow> Open = [];
    private readonly string? _reportPath;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(20) };

    public AlertToastWindow(string title, string body, string? reportPath)
    {
        InitializeComponent();
        _reportPath = reportPath;
        TitleText.Text = title;
        BodyText.Text = body;
        OpenButton.Visibility = reportPath is null ? Visibility.Collapsed : Visibility.Visible;

        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            var slot = Open.Count; // newer messages stack above older ones
            Left = area.Right - ActualWidth - 4;
            Top = Math.Max(area.Top, area.Bottom - (ActualHeight * (slot + 1)) - 4);
            Open.Add(this);
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            Open.Remove(this);
        };
        _timer.Tick += (_, _) => Close();
        _timer.Start();
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        if (_reportPath is not null) ShellOpen.Open(_reportPath);
        Close();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
