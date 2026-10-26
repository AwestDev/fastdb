using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Wpf.Controls;

public partial class PersianDateTimeValueBox : UserControl
{
    private PersianDateTimePickerControl? _picker;
    private ScrollViewer? _pickerScrollViewer;
    private Window? _ownerWindow;
    private bool _repositionQueued;

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(PersianDateTimeValueBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged, null, false, UpdateSourceTrigger.PropertyChanged));

    public PersianDateTimeValueBox()
    {
        InitializeComponent();
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PersianDateTimeValueBox box) return;
        var value = (string?)e.NewValue ?? string.Empty;
        box.DisplayBox.Text = PersianDateTimeCodec.TryParseDatabaseValue(value, out var dateTime)
            ? PersianDigits(PersianDateTimeCodec.ToPersianDisplay(dateTime))
            : value;
    }

    private static string PersianDigits(string value) => string.Concat(value.Select(c =>
        c is >= '0' and <= '9' ? (char)('\u06f0' + c - '0') : c));

    public void OpenPicker()
    {
        if (_picker is null)
        {
            _picker = new PersianDateTimePickerControl();
            _picker.ValueSelected += (_, value) =>
            {
                Value = value;
                PickerPopup.IsOpen = false;
            };
            _picker.Cancelled += (_, _) => PickerPopup.IsOpen = false;
            _pickerScrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _picker
            };
            PickerBorder.Child = _pickerScrollViewer;
        }

        UpdatePickerSize();
        _picker.SetDatabaseValue(Value);
        PickerPopup.IsOpen = true;
    }

    private void ValueBox_Loaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (ReferenceEquals(window, _ownerWindow)) return;
        DetachOwnerWindow();
        _ownerWindow = window;
        if (_ownerWindow is null) return;
        _ownerWindow.SizeChanged += OwnerWindow_SizeChanged;
        _ownerWindow.LocationChanged += OwnerWindow_LocationChanged;
    }

    private void ValueBox_Unloaded(object sender, RoutedEventArgs e)
    {
        PickerPopup.IsOpen = false;
        DetachOwnerWindow();
    }

    private void DetachOwnerWindow()
    {
        if (_ownerWindow is null) return;
        _ownerWindow.SizeChanged -= OwnerWindow_SizeChanged;
        _ownerWindow.LocationChanged -= OwnerWindow_LocationChanged;
        _ownerWindow = null;
    }

    private void OwnerWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePickerSize();
        SchedulePopupReposition();
    }

    private void OwnerWindow_LocationChanged(object? sender, EventArgs e) => SchedulePopupReposition();

    private void UpdatePickerSize()
    {
        if (_picker is null || _pickerScrollViewer is null || _ownerWindow is null) return;
        var availableWidth = Math.Max(260, _ownerWindow.ActualWidth - 48);
        var width = Math.Min(480, Math.Max(300, Math.Min(availableWidth, _ownerWindow.ActualWidth * 0.38)));
        PickerBorder.Width = width;
        _pickerScrollViewer.Width = width - 2;
        _pickerScrollViewer.MaxHeight = Math.Max(260, _ownerWindow.ActualHeight - 96);
        _picker.Width = width - 2;
    }

    private void SchedulePopupReposition()
    {
        if (!PickerPopup.IsOpen || _repositionQueued) return;
        _repositionQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _repositionQueued = false;
            if (!PickerPopup.IsOpen || !IsLoaded) return;
            var horizontalOffset = PickerPopup.HorizontalOffset;
            var verticalOffset = PickerPopup.VerticalOffset;
            PickerPopup.HorizontalOffset = horizontalOffset + 1;
            PickerPopup.VerticalOffset = verticalOffset + 1;
            PickerPopup.HorizontalOffset = horizontalOffset;
            PickerPopup.VerticalOffset = verticalOffset;
        }));
    }

    private void DisplayBox_Click(object sender, MouseButtonEventArgs e) => OpenPicker();
    private void CalendarButton_Click(object sender, RoutedEventArgs e) => OpenPicker();

    private void DisplayBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            OpenPicker();
            e.Handled = true;
        }
    }
}
