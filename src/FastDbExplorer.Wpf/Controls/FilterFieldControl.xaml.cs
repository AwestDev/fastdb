using System;
using System.Windows;
using System.Windows.Controls;
using FastDbExplorer.Wpf.Localization;

namespace FastDbExplorer.Wpf.Controls;

/// <summary>
/// A filter field control that adapts to different column data types.
/// For DateTime columns, it shows a DateTime picker.
/// For other types, it shows a text input.
/// </summary>
public partial class FilterFieldControl : UserControl
{
    public enum FieldType { Text, Number, DateTime, Boolean }

    private FieldType _fieldType;

    /// <summary>
    /// Fired when user applies a filter value.
    /// Args: (fieldName, selectedValue)
    /// </summary>
    public event EventHandler<(string, string)>? FilterApplied;

    public FilterFieldControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initialize the filter field for a specific column.
    /// </summary>
    public void Initialize(string fieldName, FieldType fieldType, string? placeholder = null)
    {
        FieldLabel.Text = fieldName;
        _fieldType = fieldType;

        switch (fieldType)
        {
            case FieldType.DateTime:
                SetupDateTimeField();
                break;
            case FieldType.Number:
                SetupNumberField();
                break;
            case FieldType.Boolean:
                SetupBooleanField();
                break;
            default:
                SetupTextField();
                break;
        }

        // WPF TextBox has no built-in placeholder; use a tooltip instead.
        if (!string.IsNullOrEmpty(placeholder))
            InputTextBox.ToolTip = placeholder;
    }

    // ======================== Setup Methods ========================
    private void SetupTextField()
    {
        FieldTypeTag.Text = "TEXT";
        FieldTypeTag.Foreground = FindResource("MutedBrush") as System.Windows.Media.Brush;

        InputTextBox.Visibility = Visibility.Visible;
        DateTimeInput.Visibility = Visibility.Collapsed;
        ActionButton.Content = "🔍";
        ActionButton.ToolTip = "Search";

        HintText.Text = "Enter text to filter by";
        HintText.Visibility = Visibility.Visible;
    }

    private void SetupNumberField()
    {
        FieldTypeTag.Text = "NUMBER";
        FieldTypeTag.Foreground = FindResource("SuccessBrush") as System.Windows.Media.Brush;

        InputTextBox.Visibility = Visibility.Visible;
        DateTimeInput.Visibility = Visibility.Collapsed;
        ActionButton.Content = "🔢";
        ActionButton.ToolTip = "Enter number";

        HintText.Text = "Supports operators: =, >, <, >=, <=, !=";
        HintText.Visibility = Visibility.Visible;
    }

    private void SetupDateTimeField()
    {
        FieldTypeTag.Text = "DATETIME";
        FieldTypeTag.Foreground = FindResource("AccentBrush") as System.Windows.Media.Brush;

        InputTextBox.Visibility = Visibility.Collapsed;
        DateTimeInput.Visibility = Visibility.Visible;
        ActionButton.Content = "📅";
        ActionButton.ToolTip = "Pick date and time";

        HintText.Text = Strings.PersianDateTimePickerHint;
        HintText.Visibility = Visibility.Visible;
    }

    private void SetupBooleanField()
    {
        FieldTypeTag.Text = "BOOLEAN";
        FieldTypeTag.Foreground = FindResource("WarningBrush") as System.Windows.Media.Brush;

        InputTextBox.Visibility = Visibility.Visible;
        DateTimeInput.Visibility = Visibility.Collapsed;
        ActionButton.Content = "✓";
        ActionButton.ToolTip = "Toggle boolean";

        HintText.Text = "true or false";
        HintText.Visibility = Visibility.Visible;
    }

    // ======================== Action Button Handler ========================
    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        switch (_fieldType)
        {
            case FieldType.DateTime:
                DateTimeInput.OpenPicker();
                break;

            case FieldType.Boolean:
                // Toggle between true/false
                InputTextBox.Text = InputTextBox.Text == "true" ? "false" : "true";
                break;

            default:
                // For text/number, focus the input
                InputTextBox.Focus();
                InputTextBox.SelectAll();
                break;
        }
    }

    // ======================== Public Methods ========================

    /// <summary>
    /// Gets the current filter value.
    /// </summary>
    public string GetValue()
    {
        return _fieldType == FieldType.DateTime
            ? DateTimeInput.Value
            : InputTextBox.Text;
    }

    /// <summary>
    /// Sets the filter value.
    /// </summary>
    public void SetValue(string value)
    {
        if (_fieldType == FieldType.DateTime)
        {
            DateTimeInput.Value = value;
        }
        else
        {
            InputTextBox.Text = value;
        }
    }

    /// <summary>
    /// Applies the current filter and fires FilterApplied event.
    /// </summary>
    public void ApplyFilter()
    {
        var value = GetValue();
        if (string.IsNullOrWhiteSpace(value))
        {
            MessageBox.Show("Please enter a filter value.", "Empty Filter", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        FilterApplied?.Invoke(this, (FieldLabel.Text, value));
    }

    /// <summary>
    /// Clears the filter value.
    /// </summary>
    public void Clear()
    {
        if (_fieldType == FieldType.DateTime)
            DateTimeInput.Value = string.Empty;
        else
            InputTextBox.Text = string.Empty;
    }
}
