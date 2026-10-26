using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FastDbExplorer.Wpf.ViewModels;

/// <summary>One searchable entry of the Ctrl+K palette: a section, a table, a monitor or a command.</summary>
public sealed record PaletteItem(string Kind, string Title, string Subtitle, string Glyph, Action Run);

/// <summary>Global search (Ctrl+K): type, arrow keys, Enter. Picking an entry runs its action after the window closes.</summary>
public sealed partial class CommandPaletteViewModel : ObservableObject
{
    private const int MaxResults = 50;
    private readonly IReadOnlyList<PaletteItem> _all;

    public CommandPaletteViewModel(IReadOnlyList<PaletteItem> items)
    {
        _all = items;
        Refresh();
    }

    public event Action? CloseRequested;

    public ObservableCollection<PaletteItem> Results { get; } = [];
    public PaletteItem? Chosen { get; private set; }

    [ObservableProperty] private string _query = "";
    [ObservableProperty] private PaletteItem? _selected;
    [ObservableProperty] private bool _hasNoResults;

    partial void OnQueryChanged(string value) => Refresh();

    public void Move(int delta)
    {
        if (Results.Count == 0) return;
        var index = Selected is null ? -1 : Results.IndexOf(Selected);
        index = Math.Clamp(index + delta, 0, Results.Count - 1);
        Selected = Results[index];
    }

    public void Confirm()
    {
        if (Selected is null) return;
        Chosen = Selected;
        CloseRequested?.Invoke();
    }

    private void Refresh()
    {
        var text = Query.Trim();
        var matches = _all
            .Where(i => text.Length == 0
                || i.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                || i.Subtitle.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Take(MaxResults)
            .ToList();

        Results.Clear();
        foreach (var match in matches) Results.Add(match);
        Selected = Results.FirstOrDefault();
        HasNoResults = Results.Count == 0;
    }
}
