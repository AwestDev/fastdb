using Microsoft.Win32;

namespace FastDbExplorer.Wpf.Services;

public interface IFileDialogService
{
    /// <summary>Returns the chosen map file path, or null if the user cancelled.</summary>
    string? PickMapFile();

    /// <summary>Returns the chosen Excel file path (.xlsx / .xls), or null if the user cancelled.</summary>
    string? PickExcelFile();

    /// <summary>Returns the path where query results should be saved, or null if the user cancelled.</summary>
    string? PickExcelSaveFile();
}

public sealed class FileDialogService : IFileDialogService
{
    public string? PickMapFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "باز کردن فایل نقشه",
            Filter = "فایل‌های نقشه|*.mbtiles;*.gmdb;*.pbf|همه‌ی فایل‌ها|*.*",
            CheckFileExists = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickExcelFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "وارد کردن فایل مختصات (Excel)",
            Filter = "فایل‌های Excel|*.xlsx;*.xls",
            CheckFileExists = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickExcelSaveFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "ذخیره‌ی نتایج در Excel",
            Filter = "فایل Excel (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            OverwritePrompt = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
