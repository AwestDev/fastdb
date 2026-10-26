using System.Globalization;
using System.IO.Compression;
using System.Text;
using FastDbExplorer.Domain;
using FastDbExplorer.Infrastructure.CoordinateLayers;

namespace FastDbExplorer.Tests;

/// <summary>Writes a minimal but valid .xlsx (one sheet, shared strings) so the tests need no Excel library.</summary>
internal static class XlsxBuilder
{
    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <param name="rows">Each cell is a string, a double or null (empty cell).</param>
    public static void Write(string path, params object?[][] rows)
    {
        var shared = new List<string>();
        int Index(string s)
        {
            var i = shared.IndexOf(s);
            if (i < 0)
            {
                shared.Add(s);
                i = shared.Count - 1;
            }

            return i;
        }

        var maxCols = rows.Length == 0 ? 1 : Math.Max(1, rows.Max(r => r.Length));
        var lastCol = (char)('A' + maxCols - 1);

        var sheet = new StringBuilder();
        sheet.Append($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"{Main}\">");
        sheet.Append($"<dimension ref=\"A1:{lastCol}{Math.Max(1, rows.Length)}\"/><sheetData>");
        for (var r = 0; r < rows.Length; r++)
        {
            sheet.Append($"<row r=\"{r + 1}\">");
            for (var c = 0; c < rows[r].Length; c++)
            {
                var cell = rows[r][c];
                if (cell is null) continue;
                var reference = $"{(char)('A' + c)}{r + 1}";
                switch (cell)
                {
                    case string s:
                        sheet.Append($"<c r=\"{reference}\" t=\"s\"><v>{Index(s)}</v></c>");
                        break;
                    case double d:
                        var number = d.ToString("R", CultureInfo.InvariantCulture);
                        sheet.Append($"<c r=\"{reference}\"><v>{number}</v></c>");
                        break;
                    default:
                        throw new ArgumentException("Only string, double and null cells are supported.");
                }
            }

            sheet.Append("</row>");
        }

        sheet.Append("</sheetData></worksheet>");

        var sst = new StringBuilder();
        sst.Append($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><sst xmlns=\"{Main}\" count=\"{shared.Count}\" uniqueCount=\"{shared.Count}\">");
        foreach (var text in shared)
            sst.Append("<si><t xml:space=\"preserve\">").Append(System.Security.SecurityElement.Escape(text)).Append("</t></si>");
        sst.Append("</sst>");

        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Add(zip, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/><Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/></Types>
            """);
        Add(zip, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
            """);
        Add(zip, "xl/workbook.xml",
            $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"{Main}\" xmlns:r=\"{RelNs}\"><sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        Add(zip, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/></Relationships>
            """);
        Add(zip, "xl/styles.xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs></styleSheet>
            """);
        Add(zip, "xl/sharedStrings.xml", sst.ToString());
        Add(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content.Trim());
    }
}

public sealed class ExcelImportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fde-excel-" + Guid.NewGuid());
    private readonly ExcelCoordinateImportService _service = new();

    private static object?[] Header => ["Name", "Latitude", "Longitude"];

    private static object?[] Row(params object?[] cells) => cells;

    private string PathFor(string fileName)
    {
        Directory.CreateDirectory(_dir);
        return Path.Combine(_dir, fileName);
    }

    private CoordinateImportResult Import(string fileName, params object?[][] rows)
    {
        var path = PathFor(fileName);
        XlsxBuilder.Write(path, rows);
        return _service.Import(path);
    }

    [Fact]
    public void Imports_valid_rows_into_a_layer()
    {
        var result = Import("Tehran Sites.xlsx", Header,
            Row("Azadi Tower", 35.6997, 51.3380),
            Row("Milad Tower", 35.7448, 51.3753));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Issues);
        Assert.Equal(2, result.RowsRead);
        Assert.Equal(2, result.RowsImported);
        Assert.Equal(0, result.RowsSkipped);

        var layer = result.Layer!;
        Assert.Equal("Tehran Sites", layer.Name);
        Assert.Equal("Tehran Sites.xlsx", layer.FileName);
        Assert.True(layer.Visibility);
        Assert.Equal(0.0, layer.RadiusMeters);
        Assert.Equal(2, layer.Points.Count);
        Assert.All(layer.Points, p => Assert.Equal(layer.Id, p.LayerId));
        Assert.Equal(2, layer.Points.Select(p => p.Id).Distinct().Count());
        Assert.Equal("Azadi Tower", layer.Points[0].Name);
        Assert.Equal(35.6997, layer.Points[0].Latitude);
        Assert.Equal(51.3380, layer.Points[0].Longitude);
    }

    [Fact]
    public async Task ImportAsync_returns_the_same_result_shape()
    {
        var path = PathFor("async.xlsx");
        XlsxBuilder.Write(path, Header, Row("A", 10.0, 20.0));

        var result = await _service.ImportAsync(path);

        Assert.True(result.Succeeded);
        Assert.Single(result.Layer!.Points);
    }

    [Fact]
    public void Header_is_case_insensitive_trimmed_and_order_independent()
    {
        var result = Import("order.xlsx",
            Row(" LONGITUDE ", "extra", "latitude", "name"),
            Row(51.5, "ignored", 35.5, "Point 1"));

        Assert.True(result.Succeeded);
        var p = Assert.Single(result.Layer!.Points);
        Assert.Equal(("Point 1", 35.5, 51.5), (p.Name, p.Latitude, p.Longitude));
    }

    [Fact]
    public void Reports_every_missing_column_at_once()
    {
        var result = Import("missing.xlsx", Row("Name", "Lat", "Lng"), Row("A", 1.0, 2.0));

        Assert.Null(result.Layer);
        Assert.Equal(2, result.Issues.Count);
        Assert.All(result.Issues, i => Assert.Equal(ImportIssueCode.MissingColumn, i.Code));
        Assert.Contains(result.Issues, i => i.Column == "Latitude");
        Assert.Contains(result.Issues, i => i.Column == "Longitude");
    }

    [Fact]
    public void Duplicate_required_column_is_an_error()
    {
        var result = Import("dup.xlsx", Row("Name", "Latitude", "Latitude", "Longitude"), Row("A", 1.0, 2.0, 3.0));

        Assert.Null(result.Layer);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(ImportIssueCode.DuplicateColumn, issue.Code);
        Assert.Equal("Latitude", issue.Column);
    }

    [Fact]
    public void Invalid_rows_are_skipped_and_reported_with_their_row_number()
    {
        var result = Import("mixed.xlsx", Header,
            Row("Ok 1", 35.0, 51.0),            // row 2: valid
            Row("Bad lat", 91.0, 51.0),         // row 3
            Row("Bad lon", 35.0, 181.0),        // row 4
            Row("Text lat", "abc", 51.0),       // row 5
            Row(null, 35.0, 51.0),              // row 6: empty name
            Row("No lat", null, 51.0),          // row 7
            Row("Edge", -90.0, 180.0));         // row 8: boundary values are valid

        Assert.True(result.Succeeded);
        Assert.Equal(7, result.RowsRead);
        Assert.Equal(2, result.RowsImported);
        Assert.Equal(5, result.RowsSkipped);
        Assert.Equal(new[] { "Ok 1", "Edge" }, result.Layer!.Points.Select(p => p.Name).ToArray());

        var byRow = result.Issues.ToDictionary(i => i.Row!.Value);
        Assert.Equal(5, byRow.Count);
        Assert.Equal(ImportIssueCode.LatitudeOutOfRange, byRow[3].Code);
        Assert.Equal(ImportIssueCode.LongitudeOutOfRange, byRow[4].Code);
        Assert.Equal(ImportIssueCode.InvalidNumber, byRow[5].Code);
        Assert.Equal(ImportIssueCode.EmptyName, byRow[6].Code);
        Assert.Equal(ImportIssueCode.MissingCoordinate, byRow[7].Code);
        Assert.Equal("Latitude", byRow[7].Column);
    }

    [Fact]
    public void Blank_rows_are_ignored_and_not_counted()
    {
        var result = Import("blank.xlsx", Header, Row("A", 1.0, 2.0), Row(), Row("B", 3.0, 4.0));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Issues);
        Assert.Equal(2, result.RowsRead);
        Assert.Equal(2, result.Layer!.Points.Count);
    }

    [Fact]
    public void Text_numbers_accept_persian_digits_and_decimal_comma()
    {
        var result = Import("text.xlsx", Header,
            Row("Persian", "\u06F3\u06F5\u066B\u06F6\u06F8\u06F9\u06F2", "\u06F5\u06F1\u066B\u06F3\u06F8\u06F9\u06F0"),
            Row("Comma", "35,5", "51,25"),
            Row("Dot as text", " 35.25 ", "51.5"));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Issues);
        var points = result.Layer!.Points;
        Assert.Equal((35.6892, 51.389), (points[0].Latitude, points[0].Longitude));
        Assert.Equal((35.5, 51.25), (points[1].Latitude, points[1].Longitude));
        Assert.Equal((35.25, 51.5), (points[2].Latitude, points[2].Longitude));
    }

    [Fact]
    public void Nan_and_infinity_text_are_rejected_and_all_invalid_means_no_layer()
    {
        var result = Import("nan.xlsx", Header, Row("nan", "NaN", 51.0), Row("inf", 35.0, "Infinity"));

        Assert.Null(result.Layer);
        Assert.Equal(2, result.RowsRead);
        Assert.Equal(2, result.Issues.Count(i => i.Code == ImportIssueCode.InvalidNumber));
        Assert.Contains(result.Issues, i => i.Code == ImportIssueCode.NoValidRows);
    }

    [Fact]
    public void Degrees_minutes_seconds_text_is_not_supported_in_phase_1()
    {
        var result = Import("dms.xlsx", Header, Row("DMS", "35\u00B041'20\"N", "51\u00B023'E"));

        Assert.Null(result.Layer);
        Assert.Contains(result.Issues, i => i.Code == ImportIssueCode.InvalidNumber);
    }

    [Fact]
    public void Header_only_sheet_has_no_data_rows()
    {
        var result = Import("header-only.xlsx", Header);

        Assert.Null(result.Layer);
        Assert.Equal(ImportIssueCode.NoDataRows, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Empty_sheet_is_reported()
    {
        var result = Import("empty.xlsx");

        Assert.Null(result.Layer);
        Assert.Equal(ImportIssueCode.EmptySheet, Assert.Single(result.Issues).Code);
    }

    [Theory]
    [InlineData("points.csv")]
    [InlineData("points.xlsm")]
    [InlineData("points.txt")]
    [InlineData("points")]
    public void Other_file_types_are_rejected(string fileName)
    {
        var path = PathFor(fileName);
        File.WriteAllText(path, "Name,Latitude,Longitude");

        var result = _service.Import(path);

        Assert.Null(result.Layer);
        Assert.Equal(ImportIssueCode.UnsupportedFileType, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Missing_file_is_reported()
    {
        var result = _service.Import(Path.Combine(_dir, "nope.xlsx"));

        Assert.Null(result.Layer);
        Assert.Equal(ImportIssueCode.FileNotFound, Assert.Single(result.Issues).Code);
    }

    [Theory]
    [InlineData("broken.xlsx")]
    [InlineData("broken.xls")]
    public void Files_that_are_not_excel_are_reported_as_corrupt(string fileName)
    {
        var path = PathFor(fileName);
        File.WriteAllText(path, "this is definitely not an Excel workbook");

        var result = _service.Import(path);

        Assert.Null(result.Layer);
        Assert.Equal(ImportIssueCode.FileCorrupt, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Xls_extension_is_accepted_and_content_decides_the_format()
    {
        // A real BIFF .xls cannot be generated here; this proves the .xls gate and the content-based detection.
        var result = Import("renamed.xls", Header, Row("A", 1.0, 2.0));

        Assert.True(result.Succeeded);
        Assert.Equal("renamed.xls", result.Layer!.FileName);
    }

    [Fact]
    public void Issue_list_is_capped()
    {
        var rows = new List<object?[]> { Header };
        for (var i = 0; i < ExcelCoordinateImportService.MaxReportedIssues + 500; i++)
            rows.Add(Row("x" + i, 95.0, 0.0));

        var result = Import("many.xlsx", rows.ToArray());

        Assert.Null(result.Layer);
        Assert.Equal(ExcelCoordinateImportService.MaxReportedIssues, result.Issues.Count);
        Assert.True(result.IssuesTruncated);
    }

    [Fact]
    public void Cancelled_token_throws()
    {
        var path = PathFor("cancel.xlsx");
        XlsxBuilder.Write(path, Header, Row("A", 1.0, 2.0));

        Assert.Throws<OperationCanceledException>(() => _service.Import(path, new CancellationToken(true)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}

public class MapLayerTests
{
    private static MapPoint Point(Guid layerId, double lat = 35, double lon = 51, string name = "P")
        => new(Guid.NewGuid(), name, lat, lon, layerId);

    [Fact]
    public void Defaults_are_visible_with_no_radius()
    {
        var id = Guid.NewGuid();
        var layer = new MapLayer(id, "L", "l.xlsx", [Point(id)]);

        Assert.True(layer.Visibility);
        Assert.Equal(0.0, layer.RadiusMeters);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(250.5)]
    [InlineData(20_037_508.0)]
    public void Valid_radius_is_stored_in_metres(double radius)
    {
        var id = Guid.NewGuid();
        var layer = new MapLayer(id, "L", "l.xlsx", [Point(id)]) { RadiusMeters = radius };

        Assert.Equal(radius, layer.RadiusMeters);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(20_037_509.0)]
    public void Invalid_radius_is_rejected(double radius)
    {
        var id = Guid.NewGuid();
        var layer = new MapLayer(id, "L", "l.xlsx", [Point(id)]);

        Assert.Throws<ArgumentOutOfRangeException>(() => layer.RadiusMeters = radius);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapLayer(id, "L", "l.xlsx", [Point(id)], true, radius));
        Assert.Equal(0.0, layer.RadiusMeters);
    }

    [Fact]
    public void A_point_of_another_layer_is_rejected()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new MapLayer(id, "L", "l.xlsx", [Point(Guid.NewGuid())]));
    }

    [Theory]
    [InlineData(91.0, 0.0)]
    [InlineData(-91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    [InlineData(0.0, -181.0)]
    [InlineData(double.NaN, 0.0)]
    public void Out_of_range_coordinates_are_rejected(double lat, double lon)
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new MapLayer(id, "L", "l.xlsx", [Point(id, lat, lon)]));
    }

    [Fact]
    public void Empty_id_name_and_file_name_are_rejected()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new MapLayer(Guid.Empty, "L", "l.xlsx", []));
        Assert.Throws<ArgumentException>(() => new MapLayer(id, " ", "l.xlsx", []));
        Assert.Throws<ArgumentException>(() => new MapLayer(id, "L", "", []));
    }
}

public sealed class JsonCoordinateLayerStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fde-layers-" + Guid.NewGuid());

    private JsonCoordinateLayerStore NewStore() => new(_dir);

    private static MapLayer Sample(string name = "Sites")
    {
        var id = Guid.NewGuid();
        return new MapLayer(id, name, "sites.xlsx",
            [
                new MapPoint(Guid.NewGuid(), "\u0646\u0642\u0637\u0647 \u06F1", 35.689197, 51.388974, id),
                new MapPoint(Guid.NewGuid(), "Sydney", -33.8688, 151.2093, id)
            ],
            visibility: false,
            radiusMeters: 1500.5);
    }

    [Fact]
    public async Task Layer_survives_a_restart_exactly()
    {
        var layer = Sample();
        await NewStore().SaveAsync(layer);

        var loaded = Assert.Single(await NewStore().LoadAllAsync()); // a brand-new store = a restarted app

        Assert.Equal(layer.Id, loaded.Id);
        Assert.Equal(layer.Name, loaded.Name);
        Assert.Equal(layer.FileName, loaded.FileName);
        Assert.False(loaded.Visibility);
        Assert.Equal(1500.5, loaded.RadiusMeters);
        Assert.Equal(layer.Points, loaded.Points); // records compare by value, including the exact doubles
    }

    [Fact]
    public async Task Saving_again_replaces_the_stored_layer()
    {
        var store = NewStore();
        var layer = Sample();
        await store.SaveAsync(layer);

        layer.Visibility = true;
        layer.RadiusMeters = 42.0;
        await store.SaveAsync(layer);

        Assert.Single(Directory.GetFiles(_dir, "*.json"));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
        var loaded = Assert.Single(await store.LoadAllAsync());
        Assert.True(loaded.Visibility);
        Assert.Equal(42.0, loaded.RadiusMeters);
    }

    [Fact]
    public async Task Point_details_remain_transient_when_a_layer_is_persisted()
    {
        var id = Guid.NewGuid();
        var layer = new MapLayer(id, "Query results", "dbo.Sample",
        [
            new MapPoint(Guid.NewGuid(), "Row 1", 35.7, 51.4, id,
                new Dictionary<string, string> { ["Secret-like query text"] = "transient" })
        ]);
        var store = NewStore();

        await store.SaveAsync(layer);

        var json = await File.ReadAllTextAsync(Directory.GetFiles(_dir, "*.json").Single());
        Assert.DoesNotContain("details", json, StringComparison.OrdinalIgnoreCase);
        Assert.Null(Assert.Single(Assert.Single(await store.LoadAllAsync()).Points).Details);
    }

    [Fact]
    public async Task Remove_deletes_the_layer()
    {
        var store = NewStore();
        var layer = Sample();
        await store.SaveAsync(layer);

        Assert.True(await store.RemoveAsync(layer.Id));
        Assert.False(await store.RemoveAsync(layer.Id));
        Assert.Empty(await store.LoadAllAsync());
    }

    [Fact]
    public async Task Missing_folder_loads_as_empty_without_creating_it()
    {
        Assert.Empty(await NewStore().LoadAllAsync());
        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public async Task Layers_are_listed_by_name()
    {
        var store = NewStore();
        await store.SaveAsync(Sample("Zeta"));
        await store.SaveAsync(Sample("alpha"));

        var names = (await store.LoadAllAsync()).Select(l => l.Name).ToArray();

        Assert.Equal(new[] { "alpha", "Zeta" }, names);
    }

    [Fact]
    public async Task Damaged_and_unknown_files_are_skipped_but_kept()
    {
        var store = NewStore();
        await store.SaveAsync(Sample());
        var corrupt = Path.Combine(_dir, "corrupt.json");
        var future = Path.Combine(_dir, "future.json");
        await File.WriteAllTextAsync(corrupt, "{ not json");
        await File.WriteAllTextAsync(future, "{\"version\":99,\"id\":\"00000000-0000-0000-0000-000000000001\"}");

        Assert.Single(await store.LoadAllAsync());
        Assert.True(File.Exists(corrupt));
        Assert.True(File.Exists(future));
    }

    [Fact]
    public async Task A_file_that_breaks_the_layer_rules_is_skipped()
    {
        var store = NewStore();
        await store.SaveAsync(Sample());
        var path = Directory.GetFiles(_dir, "*.json").Single();
        var text = await File.ReadAllTextAsync(path);
        Assert.Contains("\"radiusMeters\":1500.5", text);
        await File.WriteAllTextAsync(path, text.Replace("\"radiusMeters\":1500.5", "\"radiusMeters\":-5"));

        Assert.Empty(await store.LoadAllAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}
