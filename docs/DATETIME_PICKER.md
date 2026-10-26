# Persian Date-Time Picker

## UI

`PersianDateTimePickerControl` is a custom Solar Hijri calendar, not the WPF Gregorian `Calendar`. It shows Persian month names, starts weeks on Saturday, provides direct month/year selection and adjacent-month dates, and lets users set hours and minutes. Seconds are always stored as `00`. The picker is created only when opened, closes on outside clicks, resizes and repositions with its owner window, and scrolls when vertical space is limited. The main app starts maximized. `PersianDateTimeValueBox` displays the selected Jalali value in a popup.

## Integration

Both Explorer filters and Monitoring conditions use the same value box for the first and second values. `FilterRowViewModel.IsDateTimeField` selects it for SQL `date`, `datetime`, `datetime2`, and `smalldatetime` columns; all other types retain their existing input. The result grid also formats supported SQL `DateTime` values as Jalali dates.

```xaml
<ctrl:PersianDateTimeValueBox
    Value="{Binding Value, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
```

The monitor editor uses the same binding for `Value` and `Value2`; no separate conversion or query path is needed.

## Value Conversion

The picker keeps the selected day internally as a Gregorian `DateTime`, using `PersianCalendar` only to render/select Solar Hijri dates. The boundary codec translates a selection to the invariant SQL value and parses existing values for editing:

```csharp
var selected = PersianDateTimeCodec.FromPersianCalendar(1405, 3, 16, 22, 10, 0);
var sqlValue = PersianDateTimeCodec.ToDatabaseValue(selected);
// 2026-06-06 22:10:00
```

`PersianDateTimeCodec.TryParseDatabaseValue` accepts `yyyy-MM-dd HH:mm:ss` (and fractional seconds from `datetime2`). The filter view models continue to pass that ASCII Gregorian string to the existing typed parameter conversion; the Persian display value is never sent to SQL.

## Tests

`PersianDateTimeCodecTests` verifies Solar Hijri/Gregorian conversion, the database format, fractional-second loading, and invalid calendar dates. Build and run the full WPF application on Windows to verify popup placement and light/dark theme rendering.
