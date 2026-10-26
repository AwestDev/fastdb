using System.Media;
using FastDbExplorer.Wpf.Views;

namespace FastDbExplorer.Wpf.Services;

public interface IAlertNotifier
{
    /// <summary>Shows a message (small window at the screen corner) and optionally plays a sound.</summary>
    void Notify(string title, string body, string? reportPath, bool playSound);
}

public sealed class AlertNotifier : IAlertNotifier
{
    public void Notify(string title, string body, string? reportPath, bool playSound)
    {
        if (playSound)
        {
            try { SystemSounds.Exclamation.Play(); }
            catch (Exception) { /* no sound device: the visual message is still shown */ }
        }

        try { new AlertToastWindow(title, body, reportPath).Show(); }
        catch (Exception) { /* a failed message window must never stop the monitoring */ }
    }
}
