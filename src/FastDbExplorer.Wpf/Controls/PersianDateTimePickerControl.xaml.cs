using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using FastDbExplorer.Domain;
using FastDbExplorer.Wpf.Localization;

namespace FastDbExplorer.Wpf.Controls;

public partial class PersianDateTimePickerControl : UserControl
{
    private sealed record YearOption(int Value, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly PersianCalendar _calendar = new();
    private DateTime _selectedDate;
    private DateTime _displayMonth;
    private int _yearOptionStart;
    private int _yearOptionEnd;
    private bool _updatingTimeBoxes;
    private bool _updatingCalendarSelectors;
    // XAML can raise TextChanged before all named controls have been assigned.
    private bool _isInitialized;

    public event EventHandler<string>? ValueSelected;
    public event EventHandler? Cancelled;

    public PersianDateTimePickerControl()
    {
        InitializeComponent();
        _isInitialized = true;
        _updatingCalendarSelectors = true;
        MonthComboBox.ItemsSource = Strings.PersianMonthNames.Skip(1).ToList();
        _updatingCalendarSelectors = false;
        foreach (var box in new[] { HourBox, MinuteBox })
            DataObject.AddPastingHandler(box, Time_Pasting);
        BuildWeekdayHeaders();
        SetDatabaseValue(null);
    }

    public void SetDatabaseValue(string? value)
    {
        var dateTime = PersianDateTimeCodec.TryParseDatabaseValue(value, out var parsed)
            ? parsed
            : DateTime.Now;
        _selectedDate = dateTime.Date;
        _displayMonth = StartOfMonth(_selectedDate);
        SetTimeBoxes(dateTime.Hour, dateTime.Minute);
        RefreshCalendar();
        UpdatePreview();
    }

    private void BuildWeekdayHeaders()
    {
        WeekdayGrid.Children.Clear();
        for (var i = 0; i < Strings.PersianWeekdayLabels.Count; i++)
        {
            WeekdayGrid.Children.Add(new TextBlock
            {
                Text = Strings.PersianWeekdayLabels[i],
                ToolTip = Strings.PersianWeekdayNames[i],
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"),
                Margin = new Thickness(0, 0, 0, 4)
            });
        }
    }

    private DateTime StartOfMonth(DateTime date)
    {
        var year = _calendar.GetYear(date);
        var month = _calendar.GetMonth(date);
        return _calendar.ToDateTime(year, month, 1, 0, 0, 0, 0);
    }

    private void RefreshCalendar()
    {
        var year = _calendar.GetYear(_displayMonth);
        var month = _calendar.GetMonth(_displayMonth);
        UpdateCalendarSelectors(year, month);
        DayGrid.Children.Clear();

        var firstDay = StartOfMonth(_displayMonth);
        var leadingDays = ((int)firstDay.DayOfWeek + 1) % 7;
        var firstCell = firstDay.AddDays(-leadingDays);
        for (var i = 0; i < 42; i++)
        {
            var date = firstCell.AddDays(i);
            var inMonth = _calendar.GetYear(date) == year && _calendar.GetMonth(date) == month;
            var selected = date.Date == _selectedDate.Date;
            var button = new Button
            {
                Content = PersianDigits(_calendar.GetDayOfMonth(date).ToString("D2", CultureInfo.InvariantCulture)),
                Style = (Style)FindResource("CalendarDayButton"),
                FontWeight = selected ? FontWeights.Bold : FontWeights.Normal,
                Opacity = inMonth ? 1 : 0.42,
                ToolTip = PersianDigits(PersianDateTimeCodec.ToPersianDisplay(date))
            };
            var weekdayIndex = ((int)date.DayOfWeek + 1) % 7;
            AutomationProperties.SetName(button,
                $"{Strings.PersianWeekdayNames[weekdayIndex]} {PersianDigits(_calendar.GetDayOfMonth(date).ToString(CultureInfo.InvariantCulture))}");
            button.SetResourceReference(Control.BackgroundProperty, selected ? "AccentBrush" : "SurfaceAltBrush");
            button.SetResourceReference(Control.ForegroundProperty, selected ? "SurfaceBrush" : inMonth ? "TextBrush" : "MutedBrush");
            button.Click += (_, _) =>
            {
                _selectedDate = date.Date;
                _displayMonth = StartOfMonth(date);
                RefreshCalendar();
                UpdatePreview();
                CommitSelectedDate();
            };
            DayGrid.Children.Add(button);
        }
    }

    private static string PersianDigits(string value) => string.Concat(value.Select(c =>
        c is >= '0' and <= '9' ? (char)('\u06f0' + c - '0') : c));

    private static string AsciiDigits(string value) => string.Concat(value.Select(c =>
    {
        var digit = CharUnicodeInfo.GetDecimalDigitValue(c);
        return digit is >= 0 and <= 9 ? (char)('0' + digit) : c;
    }));

    private void UpdateCalendarSelectors(int year, int month)
    {
        _updatingCalendarSelectors = true;
        if (year < _yearOptionStart || year > _yearOptionEnd)
        {
            _yearOptionStart = Math.Max(1, year - 100);
            _yearOptionEnd = Math.Min(_calendar.GetYear(_calendar.MaxSupportedDateTime), year + 100);
            YearComboBox.ItemsSource = Enumerable.Range(_yearOptionStart, _yearOptionEnd - _yearOptionStart + 1)
                .Select(value => new YearOption(value, PersianDigits(value.ToString("D4", CultureInfo.InvariantCulture))))
                .ToList();
            YearComboBox.SelectedValuePath = nameof(YearOption.Value);
        }

        MonthComboBox.SelectedIndex = month - 1;
        YearComboBox.SelectedValue = year;
        _updatingCalendarSelectors = false;
    }

    private void SetTimeBoxes(int hour, int minute)
    {
        _updatingTimeBoxes = true;
        HourBox.Text = hour.ToString("D2", CultureInfo.InvariantCulture);
        MinuteBox.Text = minute.ToString("D2", CultureInfo.InvariantCulture);
        _updatingTimeBoxes = false;
    }

    private bool TryReadTime(out int hour, out int minute)
    {
        hour = minute = 0;
        return int.TryParse(AsciiDigits(HourBox.Text), NumberStyles.None, CultureInfo.InvariantCulture, out hour) && hour is >= 0 and <= 23
               && int.TryParse(AsciiDigits(MinuteBox.Text), NumberStyles.None, CultureInfo.InvariantCulture, out minute) && minute is >= 0 and <= 59;
    }

    private void UpdatePreview()
    {
        var valid = TryReadTime(out var hour, out var minute);
        TimeError.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
        ApplyButton.IsEnabled = valid;
        if (!valid) return;

        var selected = _selectedDate.AddHours(hour).AddMinutes(minute);
        PreviewText.Text = PersianDigits(PersianDateTimeCodec.ToPersianDisplay(selected));
    }

    private void Time_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = e.Text.Any(c => !char.IsDigit(c));

    private void Time_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(typeof(string))
            || AsciiDigits((string)e.DataObject.GetData(typeof(string))!).Any(c => !char.IsAsciiDigit(c)))
            e.CancelCommand();
    }

    private void Time_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized || _updatingTimeBoxes || sender is not TextBox box) return;
        var normalized = AsciiDigits(box.Text);
        if (normalized != box.Text)
        {
            var caret = box.CaretIndex;
            _updatingTimeBoxes = true;
            box.Text = normalized;
            box.CaretIndex = Math.Min(caret, normalized.Length);
            _updatingTimeBoxes = false;
        }
        UpdatePreview();
    }

    private void AdjustTime(TextBox box, int delta, int maximum)
    {
        var value = int.TryParse(AsciiDigits(box.Text), NumberStyles.None, CultureInfo.InvariantCulture, out var current)
            && current >= 0 && current <= maximum ? current : 0;
        box.Text = ((value + delta + maximum + 1) % (maximum + 1)).ToString("D2", CultureInfo.InvariantCulture);
        box.Focus();
    }

    private void PreviousMonth_Click(object sender, RoutedEventArgs e) => ChangeMonth(-1);
    private void NextMonth_Click(object sender, RoutedEventArgs e) => ChangeMonth(1);

    private void MonthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingCalendarSelectors || MonthComboBox.SelectedIndex < 0) return;
        SelectMonthAndYear(_calendar.GetYear(_displayMonth), MonthComboBox.SelectedIndex + 1);
    }

    private void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingCalendarSelectors || YearComboBox.SelectedValue is not int year || MonthComboBox.SelectedIndex < 0) return;
        SelectMonthAndYear(year, MonthComboBox.SelectedIndex + 1);
    }

    private void SelectMonthAndYear(int year, int month)
    {
        var day = Math.Min(_calendar.GetDayOfMonth(_selectedDate), _calendar.GetDaysInMonth(year, month));
        _selectedDate = _calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
        _displayMonth = StartOfMonth(_selectedDate);
        RefreshCalendar();
        UpdatePreview();
    }

    private void ChangeMonth(int months)
    {
        try
        {
            _displayMonth = _calendar.AddMonths(_displayMonth, months);
            _displayMonth = StartOfMonth(_displayMonth);
            RefreshCalendar();
        }
        catch (ArgumentException) { }
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        var value = PersianDateTimeCodec.ToDatabaseValue(DateTime.Now);
        SetDatabaseValue(value);
        ValueSelected?.Invoke(this, value);
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => ValueSelected?.Invoke(this, "");
    private void Cancel_Click(object sender, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        CommitSelectedDate();
    }

    private void CommitSelectedDate()
    {
        if (!TryReadTime(out var hour, out var minute))
        {
            UpdatePreview();
            return;
        }
        var value = _selectedDate.AddHours(hour).AddMinutes(minute);
        ValueSelected?.Invoke(this, PersianDateTimeCodec.ToDatabaseValue(value));
    }

    private void HourUp_Click(object sender, RoutedEventArgs e) => AdjustTime(HourBox, 1, 23);
    private void HourDown_Click(object sender, RoutedEventArgs e) => AdjustTime(HourBox, -1, 23);
    private void MinuteUp_Click(object sender, RoutedEventArgs e) => AdjustTime(MinuteBox, 1, 59);
    private void MinuteDown_Click(object sender, RoutedEventArgs e) => AdjustTime(MinuteBox, -1, 59);
}
