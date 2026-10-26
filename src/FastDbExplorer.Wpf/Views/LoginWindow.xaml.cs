using System.Windows;
using FastDbExplorer.Domain;
using FastDbExplorer.Wpf.Localization;

namespace FastDbExplorer.Wpf.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        DemoCredentialsText.Text = $"{LoginStrings.DemoCredentials}: {DemoLoginGate.UserName} / {DemoLoginGate.Password}";
        Loaded += (_, _) => PasswordInput.Focus();
    }

    private void OnSignIn(object sender, RoutedEventArgs e)
    {
        if (DemoLoginGate.IsValid(UserNameInput.Text.Trim(), PasswordInput.Password))
        {
            DialogResult = true;
            return;
        }

        PasswordInput.Clear();
        ErrorText.Visibility = Visibility.Visible;
        PasswordInput.Focus();
    }
}
