using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FastDbExplorer.Wpf.Localization;

namespace FastDbExplorer.Wpf.Views;

/// <summary>
/// The shared "show the selected rows on the map" panel. The host binds its own members to the dependency properties below;
/// this control decides the wording, the enabled state of the button and the hint, so the Explorer and Monitoring behave identically.
/// </summary>
public partial class MapSendPanel : UserControl
{
    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(nameof(Columns), typeof(IEnumerable), typeof(MapSendPanel));

    public static readonly DependencyProperty LatitudeColumnProperty = DependencyProperty.Register(
        nameof(LatitudeColumn), typeof(object), typeof(MapSendPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnStateChanged));

    public static readonly DependencyProperty LongitudeColumnProperty = DependencyProperty.Register(
        nameof(LongitudeColumn), typeof(object), typeof(MapSendPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnStateChanged));

    public static readonly DependencyProperty DetailColumnsProperty =
        DependencyProperty.Register(nameof(DetailColumns), typeof(IEnumerable), typeof(MapSendPanel));

    public static readonly DependencyProperty DetailCountProperty = DependencyProperty.Register(
        nameof(DetailCount), typeof(int), typeof(MapSendPanel), new PropertyMetadata(0, OnStateChanged));

    public static readonly DependencyProperty DetailLimitProperty = DependencyProperty.Register(
        nameof(DetailLimit), typeof(int), typeof(MapSendPanel), new PropertyMetadata(8, OnStateChanged));

    public static readonly DependencyProperty SelectedCountProperty = DependencyProperty.Register(
        nameof(SelectedCount), typeof(int), typeof(MapSendPanel), new PropertyMetadata(0, OnStateChanged));

    public static readonly DependencyProperty SendCommandProperty =
        DependencyProperty.Register(nameof(SendCommand), typeof(ICommand), typeof(MapSendPanel));

    /// <summary>False when the host knows the selection cannot be sent (for example no valid coordinates in it).</summary>
    public static readonly DependencyProperty IsSendEnabledProperty = DependencyProperty.Register(
        nameof(IsSendEnabled), typeof(bool), typeof(MapSendPanel), new PropertyMetadata(true, OnStateChanged));

    /// <summary>The per-point details row is open (closed by default; toggled by the details button).</summary>
    public static readonly DependencyProperty IsDetailsOpenProperty =
        DependencyProperty.Register(nameof(IsDetailsOpen), typeof(bool), typeof(MapSendPanel), new PropertyMetadata(false));

    // Computed by this control (read-only for the host).
    private static readonly DependencyPropertyKey CanSendKey =
        DependencyProperty.RegisterReadOnly(nameof(CanSend), typeof(bool), typeof(MapSendPanel), new PropertyMetadata(false));
    private static readonly DependencyPropertyKey HintKey =
        DependencyProperty.RegisterReadOnly(nameof(Hint), typeof(string), typeof(MapSendPanel), new PropertyMetadata(""));
    private static readonly DependencyPropertyKey HasHintKey =
        DependencyProperty.RegisterReadOnly(nameof(HasHint), typeof(bool), typeof(MapSendPanel), new PropertyMetadata(false));
    private static readonly DependencyPropertyKey HasProblemHintKey =
        DependencyProperty.RegisterReadOnly(nameof(HasProblemHint), typeof(bool), typeof(MapSendPanel), new PropertyMetadata(false));
    private static readonly DependencyPropertyKey SelectionTextKey =
        DependencyProperty.RegisterReadOnly(nameof(SelectionText), typeof(string), typeof(MapSendPanel), new PropertyMetadata(""));
    private static readonly DependencyPropertyKey SendTextKey =
        DependencyProperty.RegisterReadOnly(nameof(SendText), typeof(string), typeof(MapSendPanel), new PropertyMetadata(""));
    private static readonly DependencyPropertyKey DetailsCountTextKey =
        DependencyProperty.RegisterReadOnly(nameof(DetailsCountText), typeof(string), typeof(MapSendPanel), new PropertyMetadata(""));
    private static readonly DependencyPropertyKey DetailsNoteKey =
        DependencyProperty.RegisterReadOnly(nameof(DetailsNote), typeof(string), typeof(MapSendPanel), new PropertyMetadata(""));

    public static readonly DependencyProperty CanSendProperty = CanSendKey.DependencyProperty;
    public static readonly DependencyProperty HintProperty = HintKey.DependencyProperty;
    public static readonly DependencyProperty HasHintProperty = HasHintKey.DependencyProperty;
    public static readonly DependencyProperty HasProblemHintProperty = HasProblemHintKey.DependencyProperty;
    public static readonly DependencyProperty SelectionTextProperty = SelectionTextKey.DependencyProperty;
    public static readonly DependencyProperty SendTextProperty = SendTextKey.DependencyProperty;
    public static readonly DependencyProperty DetailsCountTextProperty = DetailsCountTextKey.DependencyProperty;
    public static readonly DependencyProperty DetailsNoteProperty = DetailsNoteKey.DependencyProperty;

    public MapSendPanel()
    {
        InitializeComponent();
        Refresh();
    }

    /// <summary>The columns the user can pick as latitude / longitude.</summary>
    public IEnumerable? Columns { get => (IEnumerable?)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    public object? LatitudeColumn { get => GetValue(LatitudeColumnProperty); set => SetValue(LatitudeColumnProperty, value); }

    public object? LongitudeColumn { get => GetValue(LongitudeColumnProperty); set => SetValue(LongitudeColumnProperty, value); }

    /// <summary>Items with <c>Name</c> and <c>IsSelected</c>: the columns shown when the mouse is over a point.</summary>
    public IEnumerable? DetailColumns { get => (IEnumerable?)GetValue(DetailColumnsProperty); set => SetValue(DetailColumnsProperty, value); }

    public int DetailCount { get => (int)GetValue(DetailCountProperty); set => SetValue(DetailCountProperty, value); }

    public int DetailLimit { get => (int)GetValue(DetailLimitProperty); set => SetValue(DetailLimitProperty, value); }

    public int SelectedCount { get => (int)GetValue(SelectedCountProperty); set => SetValue(SelectedCountProperty, value); }

    public ICommand? SendCommand { get => (ICommand?)GetValue(SendCommandProperty); set => SetValue(SendCommandProperty, value); }

    public bool IsSendEnabled { get => (bool)GetValue(IsSendEnabledProperty); set => SetValue(IsSendEnabledProperty, value); }

    public bool IsDetailsOpen { get => (bool)GetValue(IsDetailsOpenProperty); set => SetValue(IsDetailsOpenProperty, value); }

    public bool CanSend => (bool)GetValue(CanSendProperty);
    public string Hint => (string)GetValue(HintProperty);
    public bool HasHint => (bool)GetValue(HasHintProperty);
    public bool HasProblemHint => (bool)GetValue(HasProblemHintProperty);
    public string SelectionText => (string)GetValue(SelectionTextProperty);
    public string SendText => (string)GetValue(SendTextProperty);
    public string DetailsCountText => (string)GetValue(DetailsCountTextProperty);
    public string DetailsNote => (string)GetValue(DetailsNoteProperty);

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MapSendPanel)d).Refresh();

    private void OnDetailsClick(object sender, RoutedEventArgs e) => IsDetailsOpen = !IsDetailsOpen;

    /// <summary>One rule set for both screens: what the button says, whether it is on, and what hint explains why not.</summary>
    private void Refresh()
    {
        var hasColumns = LatitudeColumn is not null && LongitudeColumn is not null;
        var hasRows = SelectedCount > 0;
        var hint = !hasRows ? MapSendStrings.SelectRowsHint
            : !hasColumns ? MapSendStrings.PickColumnsHint
            : !IsSendEnabled ? MapSendStrings.InvalidCoordinatesHint
            : "";

        SetValue(CanSendKey, hasRows && hasColumns && IsSendEnabled);
        SetValue(HintKey, hint);
        SetValue(HasHintKey, hint.Length > 0);
        SetValue(HasProblemHintKey, hint.Length > 0 && hasRows); // the "select rows" hint is only a tooltip; real problems get a line
        SetValue(SelectionTextKey, hasRows ? MapSendStrings.RowsSelected(SelectedCount) : MapSendStrings.NoRowsSelected);
        SetValue(SendTextKey, hasRows ? MapSendStrings.SendRowsToMap(SelectedCount) : MapSendStrings.SendToMap);
        SetValue(DetailsCountTextKey, MapSendStrings.DetailsCount(DetailCount, DetailLimit));
        SetValue(DetailsNoteKey, MapSendStrings.DetailsNote(DetailLimit));
    }
}
