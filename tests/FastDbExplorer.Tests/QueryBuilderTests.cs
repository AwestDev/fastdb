using System.Data;
using FastDbExplorer.Domain;
using FastDbExplorer.Infrastructure;

namespace FastDbExplorer.Tests;

public class SelectQueryBuilderTests
{
    private static readonly ColumnInfo[] Table =
    [
        new("Id", "int", 1),
        new("Name", "nvarchar", 0),
        new("Code", "varchar", 0),
        new("Created", "datetime2", 0),
        new("Delete", "bit", 0)
    ];

    private static PageRequest Request(
        IReadOnlyList<FilterCondition>? filters = null, bool keyset = true, object?[]? after = null,
        IReadOnlyList<string>? order = null, long offset = 0) =>
        new("Db", "dbo", "Orders", ["Id", "Name"], filters ?? [], FilterLogic.And, order ?? ["Id"],
            keyset, 100, offset, after);

    [Fact]
    public void Builds_paged_select_with_explicit_columns_and_one_extra_row()
    {
        var q = SelectQueryBuilder.Build(Request(), Table);

        Assert.Equal(
            "SELECT [Id], [Name], [Id] AS [__k0] FROM [dbo].[Orders] ORDER BY [Id] ASC OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY",
            q.Sql);
        Assert.Equal(101, q.Parameters.Single(p => p.Name == "@take").Value);
        Assert.DoesNotContain("*", q.Sql);
    }

    [Fact]
    public void User_text_never_reaches_the_sql_text()
    {
        var evil = "x'; DROP TABLE Orders; --";
        var q = SelectQueryBuilder.Build(Request([new("Name", FilterOperator.EqualTo, evil)]), Table);

        Assert.DoesNotContain("DROP", q.Sql);
        Assert.Contains("[Name] = @p0", q.Sql);
        Assert.Equal(evil, q.Parameters.Single(p => p.Name == "@p0").Value);
        ReadOnlySqlGuard.EnsureReadOnly(q.Sql);
    }

    [Fact]
    public void Unknown_column_is_rejected()
        => Assert.Throws<ArgumentException>(() =>
            SelectQueryBuilder.Build(Request([new("Hacked; --", FilterOperator.EqualTo, "1")]), Table));

    [Fact]
    public void Text_values_are_typed_to_the_column_so_indexes_stay_usable()
    {
        var q = SelectQueryBuilder.Build(Request([new("Id", FilterOperator.GreaterThan, "42"), new("Code", FilterOperator.StartsWith, "AB")]), Table);

        Assert.Equal(SqlDbType.Int, q.Parameters[0].Type);
        Assert.Equal(42, q.Parameters[0].Value);
        Assert.Equal(SqlDbType.VarChar, q.Parameters[1].Type);
    }

    [Fact]
    public void Invalid_number_is_a_format_error()
        => Assert.Throws<FormatException>(() =>
            SelectQueryBuilder.Build(Request([new("Id", FilterOperator.EqualTo, "abc")]), Table));

    [Fact]
    public void Like_wildcards_in_user_text_are_escaped()
    {
        var q = SelectQueryBuilder.Build(Request([new("Name", FilterOperator.Contains, "50%_")]), Table);

        Assert.Contains("LIKE @p0 ESCAPE '\\'", q.Sql);
        Assert.Equal(@"%50\%\_%", q.Parameters[0].Value);
    }

    [Fact]
    public void Text_operators_are_rejected_on_numeric_columns()
        => Assert.Throws<ArgumentException>(() =>
            SelectQueryBuilder.Build(Request([new("Id", FilterOperator.Contains, "1")]), Table));

    [Fact]
    public void Between_and_in_create_one_parameter_per_value()
    {
        var between = SelectQueryBuilder.Build(Request([new("Id", FilterOperator.Between, "1", "9")]), Table);
        Assert.Contains("[Id] BETWEEN @p0 AND @p1", between.Sql);

        var list = SelectQueryBuilder.Build(Request([new("Id", FilterOperator.In, "1, 2,3")]), Table);
        Assert.Contains("[Id] IN (@p0, @p1, @p2)", list.Sql);
    }

    [Fact]
    public void Keyset_page_uses_greater_than_instead_of_offset()
    {
        var q = SelectQueryBuilder.Build(Request(after: [500]), Table);

        Assert.Contains("([Id] > @p0)", q.Sql);
        Assert.Contains("OFFSET 0 ROWS", q.Sql);
        Assert.DoesNotContain(q.Parameters, p => p.Name == "@offset");
    }

    [Fact]
    public void Legacy_paging_uses_read_only_row_number_query()
    {
        var q = SelectQueryBuilder.Build(Request(keyset: false, offset: 25), Table, supportsOffsetFetch: false);

        Assert.Contains("ROW_NUMBER() OVER (ORDER BY [Id] ASC)", q.Sql);
        Assert.Contains("WHERE [__fdb_row_number] > @offset AND [__fdb_row_number] <= @offset + @take", q.Sql);
        Assert.DoesNotContain("OFFSET", q.Sql);
        Assert.DoesNotContain("FETCH", q.Sql);
        Assert.Equal(25L, q.Parameters.Single(p => p.Name == "@offset").Value);
        Assert.Equal(101, q.Parameters.Single(p => p.Name == "@take").Value);
        ReadOnlySqlGuard.EnsureReadOnly(q.Sql);
    }

    [Fact]
    public void Legacy_keyset_page_keeps_keys_and_avoids_offset_fetch()
    {
        var q = SelectQueryBuilder.Build(Request(after: [500]), Table, supportsOffsetFetch: false);

        Assert.Contains("([Id] > @p0)", q.Sql);
        Assert.Contains("SELECT [Id], [Name], [__k0] FROM (", q.Sql);
        Assert.DoesNotContain("OFFSET", q.Sql);
        Assert.DoesNotContain("FETCH", q.Sql);
        Assert.Equal(0L, q.Parameters.Single(p => p.Name == "@offset").Value);
        ReadOnlySqlGuard.EnsureReadOnly(q.Sql);
    }

    [Fact]
    public void Legacy_paging_avoids_generated_alias_collisions()
    {
        var columns = Table.Append(new ColumnInfo("__fdb_row_number", "int", 0)).ToArray();
        var request = Request() with { Columns = ["__fdb_row_number"] };
        var q = SelectQueryBuilder.Build(request, columns, supportsOffsetFetch: false);

        Assert.Contains("ROW_NUMBER() OVER (ORDER BY [Id] ASC) AS [__fdb_row_number_1]", q.Sql);
        Assert.Contains("WHERE [__fdb_row_number_1] > @offset", q.Sql);
    }

    [Fact]
    public void Composite_key_builds_an_or_chain()
    {
        var q = SelectQueryBuilder.Build(Request(order: ["Id", "Code"], after: [5, "B"]), Table);

        Assert.Contains("([Id] > @p0) OR ([Id] = @p1 AND [Code] > @p2)", q.Sql);
    }

    [Fact]
    public void Filters_and_keyset_are_combined_with_and()
    {
        var q = SelectQueryBuilder.Build(Request([new("Name", FilterOperator.IsNull)], after: [7]), Table);

        Assert.Contains("WHERE ([Name] IS NULL) AND (([Id] > @p0))", q.Sql);
    }

    [Fact]
    public void Columns_named_like_sql_keywords_pass_the_read_only_guard()
    {
        var req = Request([new("Delete", FilterOperator.EqualTo, "1")]) with { Columns = ["Delete"] };
        ReadOnlySqlGuard.EnsureReadOnly(SelectQueryBuilder.Build(req, Table).Sql);
    }
}
