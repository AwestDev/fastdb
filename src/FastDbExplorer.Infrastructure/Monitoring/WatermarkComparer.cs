namespace FastDbExplorer.Infrastructure.Monitoring;

/// <summary>Compares two stored watermark texts as real numbers / dates (never as strings).</summary>
public static class WatermarkComparer
{
    public static int Compare(string a, string b, string typeName)
    {
        var x = (IComparable)ValueConverter.Parse(a, typeName);
        var y = ValueConverter.Parse(b, typeName);
        return x.CompareTo(y);
    }
}
