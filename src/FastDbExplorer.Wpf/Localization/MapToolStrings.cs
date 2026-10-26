using FastDbExplorer.Domain;

namespace FastDbExplorer.Wpf.Localization;

/// <summary>Texts of the map tools (go to coordinates, pick coordinates, measure distance, my points, result sets). The page receives them as "toolsInit".</summary>
public static class MapToolStrings
{
    public const string ManualLayerName = "نقاط من";
    public const string ManualPointRemoved = "نقطه از «نقاط من» حذف شد.";

    public static string ManualPointName(int number) => $"نقطه {number}";

    public static string ManualPointAdded(string name) => $"نقطهٔ «{name}» به «نقاط من» اضافه شد.";

    /// <summary>Name of a result set sent to the map: where it came from, which table, and a number when the same table was sent again with other rows.</summary>
    public static string ResultLayerName(MapLayerKind kind, string table, int ordinal)
    {
        var source = kind == MapLayerKind.Monitoring ? ShellStrings.NavMonitoring : ShellStrings.NavExplorer;
        var name = $"{source} · {table}";
        return ordinal > 1 ? $"{name} ({ordinal})" : name;
    }

    public static IReadOnlyDictionary<string, string> Labels { get; } = new Dictionary<string, string>
    {
        ["placeholder"] = "عرض و طول جغرافیایی را وارد کنید",
        ["example"] = "نمونه: 35.7219, 51.3347",
        ["go"] = "رفتن به مختصات",
        ["pick"] = "دریافت مختصات از روی نقشه",
        ["measure"] = "اندازه‌گیری فاصله",
        ["clear"] = "پاک کردن",
        ["copy"] = "کپی",
        ["copied"] = "کپی شد ✓",
        ["copyBoth"] = "کپی مختصات",
        ["lat"] = "عرض جغرافیایی",
        ["lng"] = "طول جغرافیایی",
        ["pointTitle"] = "مختصات نقطه",
        ["addPoint"] = "افزودن به نقاط من",
        ["namePlaceholder"] = "نام نقطه (اختیاری)",
        ["measureFrom"] = "اندازه‌گیری از اینجا",
        ["measureTitle"] = "اندازه‌گیری فاصله",
        ["total"] = "مسافت کل",
        ["points"] = "نقطه",
        ["lastSegment"] = "آخرین قطعه",
        ["hintStart"] = "روی نقشه کلیک کنید تا نقطهٔ شروع مشخص شود.",
        ["hintMore"] = "برای افزودن نقطهٔ بعدی کلیک کنید. دابل‌کلیک، Enter یا راست‌کلیک = پایان.",
        ["hintDone"] = "اندازه‌گیری تمام شد. برای شروع دوباره «پاک کردن» را بزنید.",
        ["undo"] = "حذف آخرین نقطه",
        ["finish"] = "پایان",
        ["close"] = "بستن",
        ["invalid"] = "مختصات معتبر نیست. نمونه: 35.7219, 51.3347",
        ["added"] = "نقطه به «نقاط من» اضافه شد",
        ["zoom"] = "نمایش روی نقشه",
        ["remove"] = "حذف از نقشه",
        ["removeAll"] = "حذف همه نتایج",
        ["deletePoint"] = "حذف این نقطه",
        ["manualPoint"] = "نقطهٔ دستی",
        ["km"] = "کیلومتر",
        ["m"] = "متر"
    };
}
