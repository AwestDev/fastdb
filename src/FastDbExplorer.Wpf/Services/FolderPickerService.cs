using System.IO;
using Microsoft.Win32;

namespace FastDbExplorer.Wpf.Services;

public interface IFolderPickerService
{
    /// <summary>Returns the chosen folder, or null if the user cancelled.</summary>
    string? PickFolder(string? initialFolder);
}

public sealed class FolderPickerService : IFolderPickerService
{
    public string? PickFolder(string? initialFolder)
    {
        var dialog = new OpenFolderDialog { Title = Localization.MonitoringStrings.PickFolderTitle, Multiselect = false };
        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
            dialog.InitialDirectory = initialFolder;
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
