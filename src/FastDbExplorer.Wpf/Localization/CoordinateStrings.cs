using System.Globalization;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Wpf.Localization;

/// <summary>
/// User-facing text of the Excel coordinate layers and the map interactions. Kept in its own file so Strings.cs stays untouched;
/// it can be merged into Strings.cs later (same pattern, same rules).
/// </summary>
public static class CoordinateStrings
{
    public const int MaxNameLength = 100;

    public const string PanelTitle = "لایه‌های مختصات (Excel)";
    public const string QueryResultLayerName = "ردیف‌های انتخاب‌شده";
    public const string ImportExcel = "وارد کردن Excel";
    public const string RadiusLabel = "شعاع اطراف هر نقطه (متر)";
    public const string RadiusTip = "عددی بین 0 تا 20,037,508؛ صفر یعنی بدون دایره";
    public const string Apply = "اعمال";
    public const string RadiusInvalid = "شعاع باید یک عدد بین 0 تا 20,037,508 متر باشد (بدون ویرگول).";
    public const string ZoomToLayer = "نمایش کل لایه";
    public const string RefreshLayer = "بازخوانی لایه از حافظه و رسم دوباره";
    public const string RemoveLayer = "حذف لایه";
    public const string LoadFailed = "خواندن لایه‌های ذخیره‌شده ممکن نشد: ";
    public const string SaveFailed = "ذخیره‌ی لایه ممکن نشد: ";
    public const string RemoveFailed = "حذف لایه ممکن نشد: ";
    public const string ImportFailedPrefix = "وارد کردن انجام نشد. ";

    // Layer management (rename / delete)
    public const string RenameLayer = "تغییر نام لایه";
    public const string SaveName = "ذخیره‌ی نام";
    public const string CancelEdit = "انصراف";
    public const string NameEmpty = "نام لایه نباید خالی باشد.";
    public const string ConfirmRemoveQuestion = "این لایه حذف شود؟";
    public const string ConfirmRemoveYes = "حذف";
    public const string ConfirmRemoveNo = "انصراف";

    // Point selection / hover
    public const string SelectedPointTitle = "نقطه‌ی انتخاب‌شده";
    public const string ClearSelection = "برداشتن انتخاب";

    // Last used map
    public const string LastMapMissingTitle = "فایل آخرین نقشه پیدا نشد";

    public static string NameTooLong() => $"نام لایه نباید بیش از {MaxNameLength} نویسه باشد.";

    public static string Summary(int points, string fileName) => $"{points:N0} نقطه · {fileName}";

    public static string PointsChip(int points) => $"{points:N0} نقطه";
    public static string QueryResultsSummary(int points) => $"{points:N0} ردیف انتخاب‌شده روی نقشه";

    public static string RadiusChip(double meters) =>
        meters > 0 ? $"شعاع {meters.ToString("#,0.###", CultureInfo.InvariantCulture)} متر" : "بدون شعاع";

    public static string Renamed(string name) => $"نام لایه به «{name}» تغییر کرد.";

    public static string HoverPoint(string name, string? layer) =>
        string.IsNullOrEmpty(layer) ? $"نقطه: {name}" : $"نقطه: {name} · {layer}";

    public static string Coordinates(double latitude, double longitude) =>
        string.Create(CultureInfo.InvariantCulture, $"{latitude:F5}, {longitude:F5}");

    public static string LastMapMissingBody(string path) =>
        "آخرین نقشه‌ای که استفاده کرده بودید دیگر در این مسیر نیست (حذف، جابه‌جا یا قطع‌شدن درایو). "
        + "مسیر ذخیره‌شده: " + path + " — یک فایل نقشه‌ی دیگر انتخاب کنید یا فایل را به مسیر قبلی برگردانید.";

    public static string Refreshed(string name) => $"لایه‌ی «{name}» دوباره خوانده و رسم شد.";

    public static string NotInStore(string name) =>
        $"لایه‌ی «{name}» در حافظه پیدا نشد؛ همان نسخه‌ی فعلی دوباره رسم شد.";

    public static string ImportFailed(IReadOnlyList<ImportIssue> issues) =>
        ImportFailedPrefix + (issues.Count > 0 ? Describe(issues[0]) : "");

    public static string Imported(int imported, int skipped, ImportIssue? firstIssue)
    {
        var text = $"{imported:N0} نقطه وارد شد.";
        if (skipped > 0)
            text += $" {skipped:N0} ردیف نامعتبر رد شد" + (firstIssue is null ? "." : $" (نمونه: {Describe(firstIssue)}).");
        return text;
    }

    /// <summary>Persian text for one import issue, with the row number when the issue belongs to a row.</summary>
    public static string Describe(ImportIssue issue)
    {
        var column = string.IsNullOrEmpty(issue.Column) ? "" : issue.Column;
        var text = issue.Code switch
        {
            ImportIssueCode.UnsupportedFileType => "فقط فایل‌های Excel با پسوند xlsx یا xls پشتیبانی می‌شوند.",
            ImportIssueCode.FileNotFound => "فایل پیدا نشد.",
            ImportIssueCode.FileUnreadable => "خواندن فایل ممکن نشد (شاید در برنامه‌ی دیگری باز است). " + issue.Detail,
            ImportIssueCode.FileCorrupt => "فایل خراب است یا فرمت معتبر Excel ندارد. " + issue.Detail,
            ImportIssueCode.PasswordProtected => "فایل با رمز عبور محافظت شده است.",
            ImportIssueCode.EmptySheet => "اولین برگه‌ی فایل خالی است.",
            ImportIssueCode.MissingColumn => $"ستون «{column}» در سطر عنوان پیدا نشد (ستون‌های لازم: Name، Latitude، Longitude).",
            ImportIssueCode.DuplicateColumn => $"ستون «{column}» بیش از یک‌بار آمده است.",
            ImportIssueCode.NoDataRows => "فایل ردیف داده ندارد.",
            ImportIssueCode.NoValidRows => "هیچ ردیف معتبری پیدا نشد.",
            ImportIssueCode.EmptyName => "نام خالی است.",
            ImportIssueCode.MissingCoordinate => $"مقدار «{column}» خالی است.",
            ImportIssueCode.InvalidNumber => $"مقدار «{column}» عدد معتبر نیست (فقط درجه‌ی اعشاری).",
            ImportIssueCode.LatitudeOutOfRange => "عرض جغرافیایی باید بین 90- تا 90 باشد.",
            ImportIssueCode.LongitudeOutOfRange => "طول جغرافیایی باید بین 180- تا 180 باشد.",
            _ => issue.Detail
        };
        return issue.Row is { } row ? $"ردیف {row}: {text}" : text.Trim();
    }
}
