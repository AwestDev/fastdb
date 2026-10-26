using System.Data;
using System.Globalization;

namespace FastDbExplorer.Infrastructure;

/// <summary>Turns text typed by the user into a correctly typed SQL parameter (so indexes stay usable).</summary>
public static class ValueConverter
{
    public const int MaxTextLength = 4000;

    public static SqlDbType GetSqlDbType(string typeName) => typeName switch
    {
        "bigint" => SqlDbType.BigInt,
        "int" => SqlDbType.Int,
        "smallint" => SqlDbType.SmallInt,
        "tinyint" => SqlDbType.TinyInt,
        "bit" => SqlDbType.Bit,
        "decimal" or "numeric" => SqlDbType.Decimal,
        "money" => SqlDbType.Money,
        "smallmoney" => SqlDbType.SmallMoney,
        "float" => SqlDbType.Float,
        "real" => SqlDbType.Real,
        "date" => SqlDbType.Date,
        "datetime" => SqlDbType.DateTime,
        "datetime2" => SqlDbType.DateTime2,
        "smalldatetime" => SqlDbType.SmallDateTime,
        "datetimeoffset" => SqlDbType.DateTimeOffset,
        "time" => SqlDbType.Time,
        "uniqueidentifier" => SqlDbType.UniqueIdentifier,
        "char" or "varchar" => SqlDbType.VarChar,
        "nchar" or "nvarchar" => SqlDbType.NVarChar,
        _ => throw new NotSupportedException($"Column type '{typeName}' is not supported for filtering or paging.")
    };

    public static object Parse(string text, string typeName)
    {
        var inv = CultureInfo.InvariantCulture;
        var t = text.Trim();
        try
        {
            return typeName switch
            {
                "bigint" => long.Parse(t, inv),
                "int" => int.Parse(t, inv),
                "smallint" => short.Parse(t, inv),
                "tinyint" => byte.Parse(t, inv),
                "bit" => ParseBit(t),
                "decimal" or "numeric" or "money" or "smallmoney" => decimal.Parse(t, NumberStyles.Number, inv),
                "float" => double.Parse(t, NumberStyles.Float, inv),
                "real" => float.Parse(t, NumberStyles.Float, inv),
                "date" or "datetime" or "datetime2" or "smalldatetime" => DateTime.Parse(t, inv, DateTimeStyles.None),
                "datetimeoffset" => DateTimeOffset.Parse(t, inv),
                "time" => TimeSpan.Parse(t, inv),
                "uniqueidentifier" => Guid.Parse(t),
                "char" or "varchar" or "nchar" or "nvarchar" => CheckLength(text),
                _ => throw new NotSupportedException($"Column type '{typeName}' is not supported for filtering.")
            };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new FormatException($"'{text}' is not a valid {typeName} value.", ex);
        }
    }

    private static bool ParseBit(string t) => t switch
    {
        "1" => true,
        "0" => false,
        _ => bool.Parse(t)
    };

    private static string CheckLength(string text)
        => text.Length <= MaxTextLength ? text : throw new FormatException($"Text is longer than {MaxTextLength} characters.");
}
