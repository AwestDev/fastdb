using System.Windows;
using System.Windows.Input;
using FastDbExplorer.Wpf.ViewModels;

namespace FastDbExplorer.Wpf.Views;

/// <summary>
/// Ctrl+K palette. A separate small window (not an overlay) because the map is a native WebView2 window:
/// nothing drawn inside the main window could appear above it.
/// </summary>
public partial class CommandPaletteWindow : Window
{
    private readonly CommandPaletteViewModel _viewModel;
    private bool _closing;

    public CommandPaletteWindow(CommandPaletteViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += () => { if (!_closing) Close(); };
        Closing += (_, _) => _closing = true;
        Deactivated += (_, _) => { if (!_closing) Close(); };
        Loaded += (_, _) => SearchBox.Focus();
        PreviewKeyDown += OnKey;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: Close(); e.Handled = true; break;
            case Key.Down: _viewModel.Move(1); e.Handled = true; break;
            case Key.Up: _viewModel.Move(-1); e.Handled = true; break;
            case Key.Enter: _viewModel.Confirm(); e.Handled = true; break;
        }
    }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e) => _viewModel.Confirm();
}
