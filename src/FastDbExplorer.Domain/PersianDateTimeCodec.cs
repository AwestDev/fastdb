using System.Globalization;

namespace FastDbExplorer.Domain;

public static class PersianDateTimeCodec
{
    public const string DatabaseFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly string[] DatabaseFormats =
    [
        DatabaseFormat,
        "yyyy-MM-dd HH:mm:ss.FFFFFFF"
    ];

    public static string ToDatabaseValue(DateTime value) =>
        value.ToString(DatabaseFormat, CultureInfo.InvariantCulture);

    public static bool TryParseDatabaseValue(string? value, out DateTime result) =>
        DateTime.TryParseExact(value, DatabaseFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out result);

    public static DateTime FromPersianCalendar(
        int year, int month, int day, int hour, int minute, int second) =>
        new PersianCalendar().ToDateTime(year, month, day, hour, minute, second, 0);

    public static (int Year, int Month, int Day) ToPersianCalendar(DateTime value)
    {
        var calendar = new PersianCalendar();
        return (calendar.GetYear(value), calendar.GetMonth(value), calendar.GetDayOfMonth(value));
    }

    public static string ToPersianDisplay(DateTime value)
    {
        var (year, month, day) = ToPersianCalendar(value);
        var culture = CultureInfo.InvariantCulture;
        return $"{year.ToString("D4", culture)}/{month.ToString("D2", culture)}/{day.ToString("D2", culture)} {value.ToString("HH:mm:ss", culture)}";
    }
}
