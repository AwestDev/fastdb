using System.Windows;

namespace FastDbExplorer.Wpf.Views;

public partial class CellValueWindow : Window
{
    private readonly string _fullValue;

    public CellValueWindow(string columnName, string fullValue, bool isNull)
    {
        InitializeComponent();
        _fullValue = fullValue;
        ColumnNameText.Text = columnName;
        ValueTextBox.Text = fullValue;
        if (isNull) NullIndicator.Visibility = Visibility.Visible;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => ValueTextBox.Focus();

    private void Copy_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(_fullValue);
}
