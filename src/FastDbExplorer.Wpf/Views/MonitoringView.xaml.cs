using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using FastDbExplorer.Wpf.ViewModels;

namespace FastDbExplorer.Wpf.Views;

public partial class MonitoringView : UserControl
{
    private MonitoringViewModel? _viewModel;

    public MonitoringView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => RebuildPreviewColumns();
        PreviewGrid.SelectionChanged += OnPreviewSelectionChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = e.NewValue as MonitoringViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RebuildPreviewColumns();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonitoringViewModel.PreviewColumns)) RebuildPreviewColumns();
    }

    private void RebuildPreviewColumns()
    {
        PreviewGrid.Columns.Clear();
        if (_viewModel is null) return;

        for (var index = 0; index < _viewModel.PreviewColumns.Count; index++)
        {
            var elementStyle = new System.Windows.Style(typeof(System.Windows.Controls.TextBlock),
                (System.Windows.Style)FindResource("CellText"));
            elementStyle.Setters.Add(new System.Windows.Setter(
                System.Windows.FrameworkElement.ToolTipProperty, new Binding()));
            PreviewGrid.Columns.Add(new DataGridTextColumn
            {
                Header = _viewModel.PreviewColumns[index],
                Binding = new Binding($"Cells[{index}]") { Mode = BindingMode.OneWay },
                ElementStyle = elementStyle,
                Width = new DataGridLength(180),
                MinWidth = 100
            });
        }
    }

    private void OnPreviewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null) return;
        var rows = PreviewGrid.SelectedItems.OfType<MonitoringResultRow>().ToList();
        _viewModel.SetSelectedResultRows(rows);
        _viewModel.UpdateSelectedResultRowCount(rows.Count); // the shared map panel shows how many rows are selected
    }
}
