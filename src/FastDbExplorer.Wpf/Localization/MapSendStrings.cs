namespace FastDbExplorer.Wpf.Localization;

/// <summary>Texts of the shared "show the selected rows on the map" panel (Explorer and Monitoring use the same wording).</summary>
public static class MapSendStrings
{
    public const string Title = "نمایش روی نقشه";
    public const string Latitude = "ستون عرض جغرافیایی";
    public const string Longitude = "ستون طول جغرافیایی";
    public const string LatitudeShort = "عرض جغرافیایی";
    public const string LongitudeShort = "طول جغرافیایی";
    public const string NoRowsSelected = "ردیفی انتخاب نشده";
    public const string SendToMap = "ارسال به نقشه";
    public const string SelectRowsHint = "ردیف‌ها را در جدول انتخاب کنید؛ برای انتخاب چند ردیف از Ctrl یا Shift استفاده کنید.";
    public const string PickColumnsHint = "ستون‌های عرض و طول جغرافیایی را انتخاب کنید.";
    public const string InvalidCoordinatesHint = "ردیف‌های انتخاب‌شده در این دو ستون مختصات معتبر ندارند.";
    public const string DetailsHeader = "جزئیات هر نقطه روی نقشه";
    public const string DetailsButton = "جزئیات نقطه";

    public static string RowsSelected(int count) => $"{count:N0} ردیف انتخاب‌شده";

    public static string SendRowsToMap(int count) => $"ارسال {count:N0} ردیف به نقشه";

    public static string DetailsCount(int selected, int limit) => $" · {selected}/{limit}";

    public static string DetailsNote(int limit) => $"حداکثر {limit} ستون؛ با نگه‌داشتن ماوس روی نقطه در نقشه نمایش داده می‌شود.";
}
