using System.IO.Compression;
using System.Text;

namespace FastDbExplorer.Infrastructure.Maps;

/// <summary>
/// Reads only the layer names from a Mapbox Vector Tile (protobuf), used when the file has no metadata.
/// MVT: Tile { repeated Layer layers = 3 }, Layer { string name = 1, ... }.
/// </summary>
public static class MvtLayerReader
{
    public static bool IsGzip(byte[] data) => data.Length > 2 && data[0] == 0x1F && data[1] == 0x8B;

    public static IReadOnlyList<string> ReadLayerNames(byte[] tile)
    {
        var data = IsGzip(tile) ? Gunzip(tile) : tile;
        var names = new List<string>();
        try
        {
            var pos = 0;
            while (pos < data.Length)
            {
                var tag = ReadVarint(data, ref pos);
                var field = tag >> 3;
                var wire = (int)(tag & 7);
                if (wire == 2)
                {
                    var length = (int)ReadVarint(data, ref pos);
                    if (length < 0 || pos + length > data.Length) break;
                    if (field == 3 && ReadName(data, pos, length) is { } name) names.Add(name);
                    pos += length;
                }
                else Skip(data, wire, ref pos);
            }
        }
        catch (FormatException) { /* damaged tile: return what we found */ }
        return names;
    }

    private static string? ReadName(byte[] data, int start, int length)
    {
        var pos = start;
        var end = start + length;
        while (pos < end)
        {
            var tag = ReadVarint(data, ref pos);
            var field = tag >> 3;
            var wire = (int)(tag & 7);
            if (wire == 2)
            {
                var len = (int)ReadVarint(data, ref pos);
                if (len < 0 || pos + len > end) return null;
                if (field == 1) return Encoding.UTF8.GetString(data, pos, len);
                pos += len;
            }
            else Skip(data, wire, ref pos);
        }
        return null;
    }

    private static void Skip(byte[] data, int wire, ref int pos)
    {
        switch (wire)
        {
            case 0: ReadVarint(data, ref pos); break; // varint value
            case 1: pos += 8; break;                  // fixed64
            case 5: pos += 4; break;                  // fixed32
            default: throw new FormatException("Unsupported protobuf wire type.");
        }
    }

    private static ulong ReadVarint(byte[] data, ref int pos)
    {
        ulong result = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            if (pos >= data.Length) throw new FormatException("Unexpected end of tile.");
            var b = data[pos++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
        }
        throw new FormatException("Varint too long.");
    }

    private static byte[] Gunzip(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
