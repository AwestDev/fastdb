using System.ComponentModel;
using System.Windows;
using FastDbExplorer.Wpf.Localization;
using FastDbExplorer.Wpf.Services;
using FastDbExplorer.Wpf.ViewModels;

namespace FastDbExplorer.Wpf.Views;

/// <summary>
/// The map in its own window, so it can be moved to another monitor. It shares the one <see cref="MapViewModel"/> with the
/// Map section: layers, the open file and the camera stay the same, only the WebView2 page is a second instance.
/// Closing waits until the WebView2 has really been disposed, so nothing is torn down inside the click that closed the window.
/// </summary>
public partial class MapWindow : Window
{
    private readonly MapViewModel _viewModel;
    private bool _closing;        // the window is already releasing its map
    private bool _closeApproved;  // the release is done: the next Close really closes

    public MapWindow(MapViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Title = MapUiStrings.MapWindowTitle(viewModel.FileName);
        // The window is left-to-right (title bar buttons on the right, like the main window); the map UI inside stays right-to-left.
        Content = new MapView { IsDetached = true, FlowDirection = FlowDirection.RightToLeft };

        SourceInitialized += (_, _) => ThemeService.SetDarkTitleBar(this, ThemeService.IsDark());
        _viewModel.DockRequested += OnDockRequested;
        _viewModel.SetPoppedOut(true);
        Closing += OnClosing;
    }

    private void OnDockRequested() => Close();

    /// <summary>
    /// First close request: cancel it, release the WebView2 (queued by the view, never inside this event), wait until it is gone,
    /// then close for real. A second request while waiting is ignored.
    /// </summary>
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeApproved) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;

        _viewModel.DockRequested -= OnDockRequested;
        if (Content is MapView view)
        {
            view.ReleaseWebView();
            await Task.WhenAny(view.Disposed, Task.Delay(TimeSpan.FromSeconds(3))); // the timeout only guards against a hang
        }

        _viewModel.SetPoppedOut(false);
        _closeApproved = true;
        Close();
    }
}
