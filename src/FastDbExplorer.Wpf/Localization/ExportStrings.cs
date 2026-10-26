using System.IO;
using FastDbExplorer.Application.Abstractions;

namespace FastDbExplorer.Wpf.Localization;

/// <summary>Texts of the Excel/CSV export and of the compact Explorer result area.</summary>
public static class ExportStrings
{
    /// <summary>Header hint: how many filters the query has (a format string, {0} = the number).</summary>
    public const string ActiveFiltersFormat = "· {0} فیلتر فعال";

    private const string OpenHint = "برای حفظ صفر ابتدایی شماره‌ها در Excel از Data ← From Text/CSV استفاده کنید.";

    /// <summary>Shown after an export that was written as CSV because the result was too big for one workbook.</summary>
    public static string CsvCompleted(QueryExportResult result)
    {
        var folder = Path.GetDirectoryName(result.Files[0]);
        return result.IsSplit
            ? $"حجم نتیجه زیاد بود؛ برای سبک ماندن فایل‌ها، {result.Rows:N0} ردیف به‌صورت CSV در {result.Files.Count} فایل ذخیره شد. پوشه: {folder} · {OpenHint}"
            : $"حجم نتیجه زیاد بود؛ {result.Rows:N0} ردیف به‌صورت یک فایل CSV ذخیره شد: {result.Files[0]} · {OpenHint}";
    }
}
