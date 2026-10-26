using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FastDbExplorer.Wpf.ViewModels;

namespace FastDbExplorer.Wpf.Views;

/// <summary>
/// WPF's PasswordBox cannot be data-bound (by design), so this code-behind mirrors it with the view model's transient
/// Password: the masked box and the "revealed" TextBox edit the same value. Attached only while the view is loaded.
/// </summary>
public partial class ConnectionsView : UserControl
{
    private ConnectionViewModel? _vm;
    private bool _syncing;

    public ConnectionsView()
    {
        InitializeComponent();
        PasswordInput.PasswordChanged += OnPasswordBoxChanged;
        Loaded += (_, _) => Attach(DataContext as ConnectionViewModel);
        Unloaded += (_, _) => Attach(null);
        DataContextChanged += (_, e) => { if (IsLoaded) Attach(e.NewValue as ConnectionViewModel); };
    }

    private void Attach(ConnectionViewModel? vm)
    {
        if (!ReferenceEquals(_vm, vm))
        {
            if (_vm is not null) _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _vm = vm;
            if (_vm is not null) _vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        SyncPasswordBox();
    }

    private void OnPasswordBoxChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing || _vm is null) return;
        _syncing = true;
        try { _vm.Password = PasswordInput.Password; }
        finally { _syncing = false; }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectionViewModel.Password))
            SyncPasswordBox();
        else if (e.PropertyName == nameof(ConnectionViewModel.IsChangingPassword) && _vm is { IsChangingPassword: true })
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => PasswordInput.Focus()));
        // The show/hide toggle deliberately keeps keyboard focus on its own button.
    }

    private void SyncPasswordBox()
    {
        if (_syncing || _vm is null) return;
        _syncing = true;
        try
        {
            if (PasswordInput.Password != _vm.Password) PasswordInput.Password = _vm.Password;
        }
        finally { _syncing = false; }
    }
}
