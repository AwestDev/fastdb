using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using FastDbExplorer.Wpf.ViewModels;

namespace FastDbExplorer.Wpf.Views;

public partial class TableQueryView : UserControl
{
    private TableQueryViewModel? _viewModel;

    public TableQueryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null) _viewModel.ResultColumnsChanged -= RebuildColumns;
        _viewModel = e.NewValue as TableQueryViewModel;
        if (_viewModel is not null)
        {
            _viewModel.ResultColumnsChanged += RebuildColumns;
            RebuildColumns(_viewModel.ResultColumns);
        }
    }

    // Result columns are only known at run time, so the grid columns are created here (pure view logic).
    private void RebuildColumns(IReadOnlyList<string> names)
    {
        ResultGrid.Columns.Clear();
        for (var i = 0; i < names.Count; i++)
        {
            var cellStyle = new Style(typeof(TextBlock), (Style)FindResource("CellText"));
            cellStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding($"[{i}].FullValue")));
            cellStyle.Setters.Add(new Setter(ToolTipService.IsEnabledProperty, new Binding($"[{i}].IsTruncated")));
            ResultGrid.Columns.Add(new DataGridTextColumn
            {
                Header = names[i],
                Binding = new Binding($"[{i}].Display"),
                ElementStyle = cellStyle,
                MinWidth = 90,
                MaxWidth = 420,
                Width = 170,
                ClipboardContentBinding = new Binding($"[{i}].FullValue")
            });
        }
    }

    private void ResultGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var cell = FindVisualAncestor<DataGridCell>(e.OriginalSource as DependencyObject);
        if (cell?.DataContext is not GridRow row) return;

        var index = ResultGrid.Columns.IndexOf(cell.Column);
        if (index < 0 || index >= row.Cells.Count) return;

        var value = row[index];
        var fullValue = value.RawValue is byte[] bytes ? "0x" + Convert.ToHexString(bytes) : value.FullValue;
        var viewer = new CellValueWindow(cell.Column.Header?.ToString() ?? string.Empty, fullValue, value.RawValue is null);
        var owner = Window.GetWindow(this);
        if (owner is not null) viewer.Owner = owner;
        e.Handled = true;
        viewer.ShowDialog();
    }

    private static T? FindVisualAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T match) return match;
            element = element switch
            {
                Visual or Visual3D => VisualTreeHelper.GetParent(element),
                FrameworkContentElement content => content.Parent,
                _ => null
            };
        }

        return null;
    }

    private void ResultGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel?.SetSelectedRows(ResultGrid.SelectedItems.OfType<GridRow>().ToArray());
    }
}
