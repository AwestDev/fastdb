using CommunityToolkit.Mvvm.ComponentModel;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Wpf.ViewModels;

public sealed record OperatorOption(FilterOperator Operator, string Label)
{
    public static IReadOnlyList<OperatorOption> All { get; } =
    [
        new(FilterOperator.EqualTo,       "مساوی با"),
        new(FilterOperator.NotEqualTo,    "مخالف"),
        new(FilterOperator.GreaterThan,   "بزرگ‌تر از"),
        new(FilterOperator.GreaterOrEqual,"بزرگ‌تر یا مساوی"),
        new(FilterOperator.LessThan,      "کوچک‌تر از"),
        new(FilterOperator.LessOrEqual,   "کوچک‌تر یا مساوی"),
        new(FilterOperator.Contains,      "شامل (متن)"),
        new(FilterOperator.StartsWith,    "شروع با (متن)"),
        new(FilterOperator.EndsWith,      "پایان با (متن)"),
        new(FilterOperator.Between,       "بین دو مقدار"),
        new(FilterOperator.In,            "یکی از (با کاما)"),
        new(FilterOperator.IsNull,        "خالی (NULL)"),
        new(FilterOperator.IsNotNull,     "غیرخالی")
    ];
}

public sealed partial class FilterRowViewModel : ObservableObject
{
    private static readonly HashSet<string> DateTimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "date", "datetime", "datetime2", "smalldatetime"
    };

    public IReadOnlyList<ColumnInfo>? ColumnMeta { get; init; }
    public string? ColumnType { get; init; }

    public FilterRowViewModel(string? column, string? columnType = null)
    {
        _column = column;
        ColumnType = columnType;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsValue))]
    [NotifyPropertyChangedFor(nameof(NeedsSecondValue))]
    [NotifyPropertyChangedFor(nameof(IsDateTimeField))]
    private string? _column;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsValue))]
    [NotifyPropertyChangedFor(nameof(NeedsSecondValue))]
    private OperatorOption _selectedOperator = OperatorOption.All[0];

    [ObservableProperty] private string _value  = "";
    [ObservableProperty] private string _value2 = "";

    public bool NeedsValue       => SelectedOperator.Operator is not (FilterOperator.IsNull or FilterOperator.IsNotNull);
    public bool NeedsSecondValue => SelectedOperator.Operator == FilterOperator.Between;

    public bool IsDateTimeField
    {
        get
        {
            // Use the generated Column property, not _column, to satisfy MVVMTK0034
            if (Column is null) return false;
            if (ColumnMeta is not null)
            {
                var col = ColumnMeta.FirstOrDefault(c => c.Name == Column);
                if (col is not null) return DateTimeTypes.Contains(col.TypeName);
            }
            if (ColumnType is not null) return DateTimeTypes.Contains(ColumnType);
            return false;
        }
    }
}
