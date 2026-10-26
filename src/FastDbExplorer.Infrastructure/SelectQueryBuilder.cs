using System.Data;
using FastDbExplorer.Domain;

namespace FastDbExplorer.Infrastructure;

public sealed record QueryParameter(string Name, SqlDbType Type, object Value);

public sealed record BuiltQuery(string Sql, IReadOnlyList<QueryParameter> Parameters);

/// <summary>
/// Builds a parameterized SELECT. Identifiers come from the real column list and are bracket-quoted;
/// values are never concatenated into SQL; no SELECT *; paging always has a deterministic ORDER BY.
/// It asks for pageSize + 1 rows so the caller can tell whether a next page exists without COUNT(*).
/// </summary>
public static class SelectQueryBuilder
{
    public const int MaxPageSize = 1000;
    public const int MaxFilters = 20;
    public const int MaxInValues = 100;

    public static string Quote(string identifier) => "[" + identifier.Replace("]", "]]") + "]";

    public static BuiltQuery Build(
        PageRequest request, IReadOnlyList<ColumnInfo> tableColumns, bool supportsOffsetFetch = true)
    {
        var byName = tableColumns.ToDictionary(c => c.Name, StringComparer.Ordinal);
        ColumnInfo Col(string name) =>
            byName.TryGetValue(name, out var c) ? c : throw new ArgumentException($"Unknown column '{name}'.");

        if (request.Columns.Count == 0) throw new ArgumentException("Select at least one column.");
        if (request.OrderBy.Count == 0) throw new ArgumentException("An ORDER BY column is required for paging.");
        if (request.Filters.Count + (request.RequiredFilters?.Count ?? 0) > MaxFilters)
            throw new ArgumentException($"At most {MaxFilters} filters are allowed.");
        if (request.AfterKey is not null && (!request.UseKeyset || request.AfterKey.Length != request.OrderBy.Count))
            throw new ArgumentException("Invalid keyset position.");

        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var parameters = new List<QueryParameter>();
        string Add(SqlDbType type, object value)
        {
            var name = "@p" + parameters.Count;
            parameters.Add(new QueryParameter(name, type, value));
            return name;
        }

        var visibleColumns = request.Columns.Select(c => Quote(Col(c).Name)).ToList();
        var usedAliases = new HashSet<string>(request.Columns.Select(c => Col(c).Name), StringComparer.OrdinalIgnoreCase);
        var select = visibleColumns.ToList();
        var keyAliases = new List<string>();
        if (request.UseKeyset)
            for (var i = 0; i < request.OrderBy.Count; i++)
            {
                var alias = Quote(UniqueAlias($"__k{i}", usedAliases));
                keyAliases.Add(alias);
                select.Add($"{Quote(Col(request.OrderBy[i]).Name)} AS {alias}"); // lets the caller read the last key
            }

        var where = new List<string>();
        var filters = request.Filters.Select(f => BuildFilter(f, Col(f.Column), Add)).ToList();
        if (filters.Count > 0)
            where.Add("(" + string.Join(request.Logic == FilterLogic.And ? " AND " : " OR ", filters) + ")");
        if (request.RequiredFilters is { Count: > 0 })
            where.AddRange(request.RequiredFilters.Select(f => BuildFilter(f, Col(f.Column), Add)).ToList());
        if (request.AfterKey is not null)
            where.Add(BuildKeyset(request, Col, Add));

        var orderBy = string.Join(", ", request.OrderBy.Select(c => Quote(Col(c).Name) + " ASC"));
        var from = $"{Quote(request.Schema)}.{Quote(request.Table)}";
        var whereSql = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "";
        var outputColumns = visibleColumns.Concat(keyAliases).ToList();
        string sql;

        if (supportsOffsetFetch)
        {
            var offset = request.AfterKey is null ? "@offset" : "0";
            sql = $"SELECT {string.Join(", ", select)} FROM {from}{whereSql} ORDER BY {orderBy} OFFSET {offset} ROWS FETCH NEXT @take ROWS ONLY";
            if (request.AfterKey is null)
                parameters.Add(new QueryParameter("@offset", SqlDbType.BigInt, Math.Max(0, request.Offset)));
            parameters.Add(new QueryParameter("@take", SqlDbType.Int, pageSize + 1));
        }
        else
        {
            var rowNumberAlias = Quote(UniqueAlias("__fdb_row_number", usedAliases));
            var inner = $"SELECT {string.Join(", ", select)}, ROW_NUMBER() OVER (ORDER BY {orderBy}) AS {rowNumberAlias} FROM {from}{whereSql}";
            var offset = request.AfterKey is null ? Math.Max(0, request.Offset) : 0;
            parameters.Add(new QueryParameter("@offset", SqlDbType.BigInt, offset));
            parameters.Add(new QueryParameter("@take", SqlDbType.Int, pageSize + 1));
            sql = $"SELECT {string.Join(", ", outputColumns)} FROM ({inner}) AS [__fdb_page] WHERE {rowNumberAlias} > @offset AND {rowNumberAlias} <= @offset + @take ORDER BY {rowNumberAlias}";
        }

        return new BuiltQuery(sql, parameters);
    }

    private static string UniqueAlias(string candidate, HashSet<string> used)
    {
        var alias = candidate;
        var suffix = 1;
        while (!used.Add(alias)) alias = $"{candidate}_{suffix++}";
        return alias;
    }

    private static string BuildKeyset(PageRequest r, Func<string, ColumnInfo> col, Func<SqlDbType, object, string> add)
    {
        string KeyParam(int index)
        {
            var column = col(r.OrderBy[index]);
            var value = r.AfterKey![index] ?? throw new ArgumentException("Key value cannot be null.");
            return add(ValueConverter.GetSqlDbType(column.TypeName), value);
        }

        // (k0 > a0) OR (k0 = a0 AND k1 > a1) OR ...  — works for single and composite keys.
        var terms = new List<string>();
        for (var i = 0; i < r.OrderBy.Count; i++)
        {
            var parts = new List<string>();
            for (var j = 0; j < i; j++)
                parts.Add($"{Quote(col(r.OrderBy[j]).Name)} = {KeyParam(j)}");
            parts.Add($"{Quote(col(r.OrderBy[i]).Name)} > {KeyParam(i)}");
            terms.Add("(" + string.Join(" AND ", parts) + ")");
        }
        return "(" + string.Join(" OR ", terms) + ")";
    }

    private static string BuildFilter(FilterCondition f, ColumnInfo col, Func<SqlDbType, object, string> add)
    {
        var c = Quote(col.Name);
        if (f.Operator == FilterOperator.IsNull) return $"{c} IS NULL";
        if (f.Operator == FilterOperator.IsNotNull) return $"{c} IS NOT NULL";

        if (!col.IsFilterable)
            throw new NotSupportedException($"Column '{col.Name}' ({col.TypeName}) cannot be filtered.");
        if (string.IsNullOrWhiteSpace(f.Value))
            throw new ArgumentException($"A value is required for column '{col.Name}'.");

        var type = ValueConverter.GetSqlDbType(col.TypeName);
        string One(string text) => add(type, ValueConverter.Parse(text, col.TypeName));

        switch (f.Operator)
        {
            case FilterOperator.EqualTo: return $"{c} = {One(f.Value)}";
            case FilterOperator.NotEqualTo: return $"{c} <> {One(f.Value)}";
            case FilterOperator.GreaterThan: return $"{c} > {One(f.Value)}";
            case FilterOperator.GreaterOrEqual: return $"{c} >= {One(f.Value)}";
            case FilterOperator.LessThan: return $"{c} < {One(f.Value)}";
            case FilterOperator.LessOrEqual: return $"{c} <= {One(f.Value)}";

            case FilterOperator.Between:
                if (string.IsNullOrWhiteSpace(f.Value2))
                    throw new ArgumentException($"A second value is required for column '{col.Name}'.");
                return $"{c} BETWEEN {One(f.Value)} AND {One(f.Value2)}";

            case FilterOperator.In:
                var items = f.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (items.Length == 0 || items.Length > MaxInValues)
                    throw new ArgumentException($"IN needs 1 to {MaxInValues} values.");
                return $"{c} IN ({string.Join(", ", items.Select(One))})";

            case FilterOperator.Contains:
            case FilterOperator.StartsWith:
            case FilterOperator.EndsWith:
                if (!col.IsText)
                    throw new ArgumentException($"Text operators need a text column; '{col.Name}' is {col.TypeName}.");
                var escaped = EscapeLike(f.Value);
                var pattern = f.Operator switch
                {
                    FilterOperator.Contains => $"%{escaped}%",
                    FilterOperator.StartsWith => $"{escaped}%",
                    _ => $"%{escaped}"
                };
                return $"{c} LIKE {add(type, pattern)} ESCAPE '\\'";

            default:
                throw new ArgumentOutOfRangeException(nameof(f), f.Operator, "Unknown operator.");
        }
    }

    private static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
