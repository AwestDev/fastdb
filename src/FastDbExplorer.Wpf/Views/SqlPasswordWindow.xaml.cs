using System.Windows;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Wpf.Views;

public partial class SqlPasswordWindow : Window
{
    public SqlPasswordWindow(SavedConnection profile)
    {
        InitializeComponent();
        ProfileText.Text = $"{profile.Name} · {profile.Server}";
        Loaded += (_, _) => PasswordInput.Focus();
    }

    public string? Password { get; private set; }

    private void OnConnect(object sender, RoutedEventArgs e)
    {
        Password = PasswordInput.Password;
        DialogResult = true;
    }
}
