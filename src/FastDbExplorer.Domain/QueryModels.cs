namespace FastDbExplorer.Domain;

public sealed record ColumnInfo(string Name, string TypeName, int KeyOrdinal)
{
    private static readonly HashSet<string> TextTypes = ["char", "varchar", "nchar", "nvarchar"];

    private static readonly HashSet<string> OtherFilterable =
    [
        "bigint", "int", "smallint", "tinyint", "bit", "decimal", "numeric", "money", "smallmoney", "float", "real",
        "date", "datetime", "datetime2", "smalldatetime", "datetimeoffset", "time", "uniqueidentifier"
    ];

    public bool IsPrimaryKey => KeyOrdinal > 0;
    public bool IsText => TextTypes.Contains(TypeName);

    /// <summary>Types we can safely turn into typed SQL parameters (keeps predicates index-friendly).</summary>
    public bool IsFilterable => IsText || OtherFilterable.Contains(TypeName);
}

public enum FilterOperator
{
    EqualTo, NotEqualTo, GreaterThan, GreaterOrEqual, LessThan, LessOrEqual,
    Contains, StartsWith, EndsWith, Between, In, IsNull, IsNotNull
}

public enum FilterLogic { And, Or }

public sealed record FilterCondition(string Column, FilterOperator Operator, string? Value = null, string? Value2 = null);

/// <summary>
/// One page request. Paging is OFFSET-based when <see cref="AfterKey"/> is null,
/// otherwise keyset-based ("rows after this key"), which stays fast on very deep pages.
/// <see cref="RequiredFilters"/> are always AND-ed with the user filters (used by monitors for the "since last check" window).
/// </summary>
public sealed record PageRequest(
    string Database,
    string Schema,
    string Table,
    IReadOnlyList<string> Columns,
    IReadOnlyList<FilterCondition> Filters,
    FilterLogic Logic,
    IReadOnlyList<string> OrderBy,
    bool UseKeyset,
    int PageSize,
    long Offset,
    object?[]? AfterKey,
    IReadOnlyList<FilterCondition>? RequiredFilters = null);

public sealed record PageResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<object?[]> Rows,
    bool HasMore,
    object?[]? LastKey);
