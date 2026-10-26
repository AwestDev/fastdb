using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using FastDbExplorer.Wpf.ViewModels;

namespace FastDbExplorer.Wpf.Views;

public partial class ConnectionView : UserControl
{
    public ConnectionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // PasswordBox cannot be data-bound by design, so this small bridge keeps the VM in sync.
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ConnectionViewModel vm && vm.Password != PasswordInput.Password)
            vm.Password = PasswordInput.Password;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ConnectionViewModel vm)
        {
            PasswordInput.Password = vm.Password;
            vm.PropertyChanged += OnViewModelChanged;
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectionViewModel.Password) && sender is ConnectionViewModel vm
            && PasswordInput.Password != vm.Password)
            PasswordInput.Password = vm.Password;
    }
}
