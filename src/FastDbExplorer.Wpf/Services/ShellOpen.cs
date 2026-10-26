using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace FastDbExplorer.Wpf.Services;

/// <summary>Opens a report with the default program, or shows it in Explorer. Returns an error text, or null on success.</summary>
public static class ShellOpen
{
    public static string? Open(string path)
    {
        try
        {
            if (!File.Exists(path)) return path;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return ex.Message;
        }
    }

    public static string? Reveal(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return ex.Message;
        }
    }
}
