using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FastDbExplorer.Domain;
using FastDbExplorer.Wpf.Localization;
using FastDbExplorer.Wpf.Services;
using FastDbExplorer.Wpf.ViewModels;

namespace FastDbExplorer.Wpf;

public partial class MainWindow : Window
{
    // The one map view of the shell: created on the first visit to the Map section, then only hidden/shown.
    private Views.MapView? _mapView;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => ThemeService.SetDarkTitleBar(this, ThemeService.IsDark());
        StateChanged += (_, _) => UpdateMaximizeState();
        PreviewKeyDown += OnSectionShortcut;
        viewModel.PaletteRequested += OnPaletteRequested;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentView)) SyncMapHost();
        };
        Loaded += async (_, _) =>
        {
            UpdateMaximizeState();
            await viewModel.InitializeAsync();
        };
    }

    /// <summary>
    /// Rebuilding the map (a WebView2 plus the map page and its first tiles) on every visit took seconds. The map view is created
    /// once, when the Map section is first shown, and afterwards only hidden/shown, so coming back is instant. A view whose WebView2
    /// was released (the map was moved to its own window) is replaced by a fresh one.
    /// </summary>
    private void SyncMapHost()
    {
        if (DataContext is not MainViewModel viewModel) return;

        if (viewModel.CurrentView is MapViewModel map)
        {
            if (_mapView is null || _mapView.IsReleased)
            {
                if (_mapView is not null) MapHost.Children.Remove(_mapView);
                _mapView = new Views.MapView { DataContext = map };
                MapHost.Children.Add(_mapView);
            }

            _mapView.Visibility = Visibility.Visible;
            _mapView.OnShown();
        }
        else if (_mapView is not null)
        {
            _mapView.Visibility = Visibility.Collapsed;
        }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeRestore(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    /// <summary>Ctrl+K opens the search palette; Ctrl+1..Ctrl+5 jump through the navigation rail. (Not while the map has keyboard focus: WebView2 is a native window.)</summary>
    private void OnSectionShortcut(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (DataContext is not MainViewModel viewModel) return;

        if (e.Key == Key.K)
        {
            viewModel.OpenPaletteCommand.Execute(null);
            e.Handled = true;
            return;
        }

        AppSection? section = e.Key switch
        {
            Key.D1 or Key.NumPad1 => AppSection.Home,
            Key.D2 or Key.NumPad2 => AppSection.Connections,
            Key.D3 or Key.NumPad3 => AppSection.Explorer,
            Key.D4 or Key.NumPad4 => AppSection.Map,
            Key.D5 or Key.NumPad5 => AppSection.Monitoring,
            _ => null
        };
        if (section is null) return;

        viewModel.NavigateCommand.Execute(section.Value);
        e.Handled = true;
    }

    private async void OnActiveProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.SelectedItem is not SavedConnection profile
            || DataContext is not MainViewModel viewModel || viewModel.ActiveProfile?.Id == profile.Id)
            return;

        var connected = await viewModel.SwitchProfileAsync(profile);
        if (connected) return;

        combo.SelectedItem = viewModel.ActiveProfile;

        // No password popup: a profile without a usable stored password is completed in the Connections form.
        if (viewModel.ConnectionManager.NeedsPasswordInput)
        {
            viewModel.NavigateCommand.Execute(AppSection.Connections);
            return;
        }

        if (!string.IsNullOrWhiteSpace(viewModel.ConnectionManager.ErrorMessage))
            MessageBox.Show(this, viewModel.ConnectionManager.ErrorMessage, WindowStrings.AppName,
                MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
    }

    private void OnPaletteRequested(CommandPaletteViewModel palette)
    {
        new Views.CommandPaletteWindow(palette) { Owner = this }.ShowDialog();
        palette.Chosen?.Run(); // runs after the palette window is gone, so focus returns to the main window first
    }

    /// <summary>A maximized chrome window extends past the screen edge by the resize border; pull the content back in.</summary>
    private void UpdateMaximizeState()
    {
        var maximized = WindowState == WindowState.Maximized;
        RootGrid.Margin = maximized ? SystemParameters.WindowResizeBorderThickness : new Thickness(0);
        MaxRestoreButton.Content = maximized ? "\uE923" : "\uE922";
        MaxRestoreButton.ToolTip = maximized ? WindowStrings.Restore : WindowStrings.Maximize;
    }
}
