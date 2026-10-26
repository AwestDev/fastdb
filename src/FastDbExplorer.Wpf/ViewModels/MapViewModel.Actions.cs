namespace FastDbExplorer.Wpf.ViewModels;

/// <summary>
/// A one-shot action that waits for the map page. Switching to the Map section used to build a brand-new view and page, so a
/// command sent right away (for example "zoom to this layer" from the Ctrl+K palette) would be dropped: the page was not ready yet.
/// The map view now stays alive while the user visits other sections, so when the page is already ready the action runs at once.
/// </summary>
public sealed partial class MapViewModel
{
    private Action? _afterPageReady;

    /// <summary>True while a map page is loaded and has reported "ready" (set by the view).</summary>
    public bool IsPageReady { get; private set; }

    public void SetPageReady(bool ready) => IsPageReady = ready;

    /// <summary>Runs <paramref name="action"/> once: now when the page is ready, otherwise right after the next time it reports "ready" (replaces an earlier waiting action).</summary>
    public void RunWhenPageReady(Action action)
    {
        if (IsPageReady) { action(); return; }
        _afterPageReady = action;
    }

    /// <summary>Called by the view when the page is ready and the source and coordinate layers have been sent.</summary>
    public void OnPageReady()
    {
        var action = _afterPageReady;
        _afterPageReady = null;
        action?.Invoke();
    }
}
