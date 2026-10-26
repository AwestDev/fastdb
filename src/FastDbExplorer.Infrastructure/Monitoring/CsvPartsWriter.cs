using System.Globalization;
using System.Text;
using FastDbExplorer.Application.Abstractions;

namespace FastDbExplorer.Infrastructure.Monitoring;

/// <summary>
/// Writes a big result as CSV, split into parts so no file gets heavy: UTF-8 with a byte-order mark (Excel shows Persian text
/// correctly), comma separated, CRLF line ends, the header repeated in every part, invariant number and date formats.
/// A new part starts after <see cref="ExportLimits.CsvMaxRowsPerFile"/> rows or <see cref="ExportLimits.CsvMaxBytesPerFile"/> bytes.
/// Parts are named <c>name_part001.csv</c>, <c>name_part002.csv</c> ...; when everything fits in one part it is simply <c>name.csv</c>.
/// Existing files are never overwritten (a time stamp is added to the name instead). If anything fails or is cancelled, every
/// file written so far is deleted, so no half export stays behind.
/// </summary>
internal sealed class CsvPartsWriter : IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8Bom = new(true);
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly string _folder;
    private readonly string _baseName;
    private readonly IReadOnlyList<string> _columns;
    private readonly ExportLimits _limits;
    private readonly List<string> _finished = [];
    private StreamWriter? _writer;
    private string? _tempPath;
    private string? _finalPath;
    private long _partRows;
    private long _partBytes;
    private int _partNumber;
    private bool _completed;

    public CsvPartsWriter(string folder, string baseName, IReadOnlyList<string> columns, ExportLimits limits)
    {
        _folder = folder;
        _columns = columns;
        _limits = limits;
        _baseName = UniqueBase(folder, baseName);
    }

    /// <summary>Data rows written so far (all parts).</summary>
    public long RowCount { get; private set; }

    public async Task WriteRowAsync(object?[] row, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_writer is null || _partRows >= _limits.CsvMaxRowsPerFile || _partBytes >= _limits.CsvMaxBytesPerFile)
            await NextPartAsync(ct).ConfigureAwait(false);

        var line = BuildLine(row.Select(Text));
        await _writer!.WriteAsync(line.AsMemory(), ct).ConfigureAwait(false);
        _partRows++;
        RowCount++;
        _partBytes += Encoding.UTF8.GetByteCount(line);
    }

    /// <summary>Closes the last part and returns every file, in order.</summary>
    public async Task<IReadOnlyList<string>> CompleteAsync(CancellationToken ct)
    {
        if (_writer is null && _finished.Count == 0) await NextPartAsync(ct).ConfigureAwait(false); // no rows at all: a header-only file
        await ClosePartAsync().ConfigureAwait(false);

        if (_finished.Count == 1)
        {
            var single = Path.Combine(_folder, _baseName + ".csv");
            File.Move(_finished[0], single, false);
            _finished[0] = single;
        }

        _completed = true;
        return _finished.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        if (_completed) return;
        try
        {
            if (_writer is not null) await _writer.DisposeAsync().ConfigureAwait(false);
        }
        catch (IOException) { /* the file is deleted below anyway */ }

        TryDelete(_tempPath);
        foreach (var file in _finished) TryDelete(file);
    }

    private async Task NextPartAsync(CancellationToken ct)
    {
        await ClosePartAsync().ConfigureAwait(false);
        _partNumber++;
        _finalPath = Path.Combine(_folder, $"{_baseName}_part{_partNumber:D3}.csv");
        _tempPath = _finalPath + ".tmp";
        _writer = new StreamWriter(
            new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true), Utf8Bom);

        var header = BuildLine(_columns);
        await _writer.WriteAsync(header.AsMemory(), ct).ConfigureAwait(false);
        _partRows = 0;
        _partBytes = Encoding.UTF8.GetByteCount(header);
    }

    private async Task ClosePartAsync()
    {
        if (_writer is null) return;
        await _writer.FlushAsync().ConfigureAwait(false);
        await _writer.DisposeAsync().ConfigureAwait(false);
        _writer = null;
        File.Move(_tempPath!, _finalPath!, true);
        _finished.Add(_finalPath!);
    }

    private static string BuildLine(IEnumerable<string?> fields)
    {
        var sb = new StringBuilder();
        var first = true;
        foreach (var field in fields)
        {
            if (!first) sb.Append(',');
            first = false;
            AppendField(sb, field);
        }
        return sb.Append("\r\n").ToString();
    }

    private static void AppendField(StringBuilder sb, string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var needsQuotes = text.AsSpan().IndexOfAny(",\"\r\n") >= 0 || text[0] == ' ' || text[^1] == ' ';
        if (!needsQuotes)
        {
            sb.Append(text);
            return;
        }
        sb.Append('"').Append(text.Replace("\"", "\"\"")).Append('"');
    }

    private static string? Text(object? value) => value switch
    {
        null or DBNull => null,
        bool b => b ? "1" : "0",
        string s => XlsxReportWriter.Clean(s),
        byte or short or int or long or decimal => Convert.ToString(value, Inv),
        double d when double.IsFinite(d) => d.ToString("R", Inv),
        float f when float.IsFinite(f) => f.ToString("R", Inv),
        _ => XlsxReportWriter.Clean(XlsxReportWriter.Describe(value))
    };

    /// <summary>A name that does not collide with an existing export (never overwrite someone's file).</summary>
    private static string UniqueBase(string folder, string baseName)
    {
        bool Taken(string name) =>
            File.Exists(Path.Combine(folder, name + ".csv")) || File.Exists(Path.Combine(folder, name + "_part001.csv"));

        if (!Taken(baseName)) return baseName;
        var stamped = baseName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv);
        var candidate = stamped;
        for (var n = 2; Taken(candidate); n++) candidate = $"{stamped}_{n}";
        return candidate;
    }

    private static void TryDelete(string? path)
    {
        try
        {
            if (path is not null && File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // nothing more can be done
        }
    }
}
