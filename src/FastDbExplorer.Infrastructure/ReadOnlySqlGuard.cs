using System.Text.RegularExpressions;

namespace FastDbExplorer.Infrastructure;

/// <summary>
/// Last line of defence for the read-only rule. Conservative on purpose:
/// anything that is not a single SELECT/WITH statement without write keywords is rejected.
/// Quoted identifiers and string literals are ignored, so a column called [Update] is fine.
/// </summary>
public static class ReadOnlySqlGuard
{
    private static readonly Regex Quoted = new(
        @"\[(?:[^\]]|\]\])*\]|'(?:[^']|'')*'", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Forbidden = new(
        @"\b(INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|TRUNCATE|MERGE|EXEC|EXECUTE|GRANT|REVOKE|DENY|INTO)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void EnsureReadOnly(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new InvalidOperationException("Empty SQL is not allowed.");

        var text = sql.Trim().TrimEnd(';').Trim();
        var startsWithRead = text.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                             || text.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);
        if (!startsWithRead)
            throw new InvalidOperationException("Only SELECT statements are allowed.");

        var stripped = Quoted.Replace(text, "[]");
        if (stripped.Contains(';'))
            throw new InvalidOperationException("Multiple statements are not allowed.");
        if (Forbidden.IsMatch(stripped))
            throw new InvalidOperationException("The statement contains a forbidden keyword.");
    }
}
