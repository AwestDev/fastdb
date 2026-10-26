using System.Runtime.CompilerServices;
using System.Text;
using FastDbExplorer.Application.Abstractions;
using FastDbExplorer.Infrastructure.Monitoring;
using Xunit;

namespace FastDbExplorer.Tests;

public sealed class QueryExportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fdbx-export-" + Guid.NewGuid().ToString("N"));
    private static readonly string[] Columns = ["Id", "Name", "Note", "Text", "When", "Score"]; // 6 columns

    // 20 cells = 3 rows of 6 columns fit one workbook; a CSV part holds `csvRowsPerFile` rows.
    private static XlsxReportWriter Writer(int csvRowsPerFile = 3)
        => new(new ExportLimits(XlsxMaxCells: 20, CsvMaxRowsPerFile: csvRowsPerFile, CsvMaxBytesPerFile: 10_000_000));

    private static async IAsyncEnumerable<object?[]> Rows(int count, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        for (var i = 1; i <= count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return new object?[] { i, "name " + i, null, "plain", new DateTime(2026, 1, 2, 3, 4, 5), 1.5 };
        }
    }

    [Fact]
    public async Task A_small_result_is_one_xlsx_file()
    {
        var path = Path.Combine(_dir, "out.xlsx");

        var result = await Writer().WriteQueryAsync(Columns, Rows(3), path, default);

        Assert.Equal(QueryExportFormat.Xlsx, result.Format);
        Assert.Equal(path, Assert.Single(result.Files));
        Assert.Equal(3, result.Rows);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task A_big_result_becomes_csv_parts_with_the_header_in_every_part()
    {
        var result = await Writer().WriteQueryAsync(Columns, Rows(10), Path.Combine(_dir, "out.xlsx"), default);

        Assert.Equal(QueryExportFormat.Csv, result.Format);
        Assert.Equal(10, result.Rows);
        Assert.Equal(4, result.Files.Count);
        Assert.True(result.IsSplit);
        Assert.EndsWith("out_part001.csv", result.Files[0]);
        Assert.EndsWith("out_part004.csv", result.Files[3]);

        var dataRows = result.Files.Select(file => File.ReadAllLines(file).Length - 1).ToArray(); // minus the header
        Assert.Equal([3, 3, 3, 1], dataRows);
        Assert.All(result.Files, file => Assert.StartsWith("Id,Name,Note,Text,When,Score", File.ReadAllText(file)));

        Assert.False(File.Exists(Path.Combine(_dir, "out.xlsx")));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public async Task A_big_result_that_fits_one_csv_file_gets_a_plain_name()
    {
        var result = await Writer(csvRowsPerFile: 100).WriteQueryAsync(Columns, Rows(5), Path.Combine(_dir, "out.xlsx"), default);

        Assert.Equal(QueryExportFormat.Csv, result.Format);
        var file = Assert.Single(result.Files);
        Assert.Equal(Path.Combine(_dir, "out.csv"), file);
        Assert.Equal(5, File.ReadAllLines(file).Length - 1);
    }

    [Fact]
    public async Task An_existing_csv_is_never_overwritten()
    {
        Directory.CreateDirectory(_dir);
        var existing = Path.Combine(_dir, "out.csv");
        await File.WriteAllTextAsync(existing, "keep me");

        var result = await Writer(csvRowsPerFile: 100).WriteQueryAsync(Columns, Rows(5), Path.Combine(_dir, "out.xlsx"), default);

        Assert.NotEqual(existing, Assert.Single(result.Files));
        Assert.Equal("keep me", await File.ReadAllTextAsync(existing));
    }

    [Fact]
    public async Task Csv_is_utf8_with_bom_and_quotes_commas_quotes_and_line_breaks()
    {
        // 2 columns, 2 cells allowed: a second row already makes the result "big".
        var writer = new XlsxReportWriter(new ExportLimits(XlsxMaxCells: 2, CsvMaxRowsPerFile: 100));
        var path = Path.Combine(_dir, "out.xlsx");

        async IAsyncEnumerable<object?[]> Tricky()
        {
            await Task.Yield();
            yield return new object?[] { 1, "a,b \"q\"\nline" };
            yield return new object?[] { 2, "plain" };
        }

        var result = await writer.WriteQueryAsync(["Id", "Text"], Tricky(), path, default);

        var file = Assert.Single(result.Files);
        Assert.Equal([0xEF, 0xBB, 0xBF], File.ReadAllBytes(file).Take(3).ToArray());
        Assert.Equal("Id,Text\r\n1,\"a,b \"\"q\"\"\nline\"\r\n2,plain\r\n", File.ReadAllText(file, Encoding.UTF8));
    }

    [Fact]
    public async Task Cancelling_leaves_no_partial_files_behind()
    {
        using var cts = new CancellationTokenSource();

        async IAsyncEnumerable<object?[]> Endless([EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            for (var i = 1; ; i++)
            {
                if (i == 8) cts.Cancel();
                ct.ThrowIfCancellationRequested();
                yield return new object?[] { i, "x", null, "y", null, 1 };
            }
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Writer().WriteQueryAsync(Columns, Endless(), Path.Combine(_dir, "out.xlsx"), cts.Token));

        Assert.True(!Directory.Exists(_dir) || Directory.GetFiles(_dir).Length == 0);
    }

    [Fact]
    public async Task The_monitoring_report_follows_the_same_rule()
    {
        var writer = Writer();
        var small = new List<object?[]> { new object?[] { 1, "a", null, "b", null, 2 } };
        var big = Enumerable.Range(1, 10).Select(i => new object?[] { i, "a", null, "b", null, 2 }).ToList();

        var smallPath = await writer.WriteAsync("m", DateTime.UtcNow, Columns, small, _dir, default);
        var bigPath = await writer.WriteAsync("m", DateTime.UtcNow, Columns, big, _dir, default);

        Assert.EndsWith(".xlsx", smallPath);
        Assert.EndsWith(".csv", bigPath);
        Assert.True(File.Exists(bigPath));
    }

    [Fact]
    public void The_workbook_row_limit_depends_on_the_number_of_columns()
    {
        var limits = ExportLimits.Default;

        Assert.Equal(100_000, limits.MaxWorkbookRows(20));
        Assert.Equal(ExportLimits.ExcelMaxDataRows, limits.MaxWorkbookRows(1)); // never above what a sheet can hold
        Assert.Equal(2_000_000, limits.XlsxMaxCells);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
