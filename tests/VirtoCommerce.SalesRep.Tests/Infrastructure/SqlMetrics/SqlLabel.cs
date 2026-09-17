using System.Text.RegularExpressions;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure.SqlMetrics;

/// <summary>
/// Turns a statement into a short label such as <c>SELECT Item</c>, which is what the report shows in its name
/// column — the full text is still there, but forty identical labels under one test name an n+1 at a glance.
/// </summary>
internal static partial class SqlLabel
{
    private const int ScanLimit = 8000;

    public static string Describe(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return "sql";
        }

        var head = sql.Length > ScanLimit ? sql[..ScanLimit] : sql;

        var verb = VerbPattern().Match(head);
        var table = TablePattern().Match(head);

        var label = verb.Success ? verb.Groups[1].Value.ToUpperInvariant() : "SQL";

        if (table.Success)
        {
            label += " " + table.Groups["name"].Value;
        }

        // One command can carry a whole batch of writes, and then its weight is in the count
        var statements = CountStatements(head);

        return statements > 1 ? label + " +" + (statements - 1) : label;
    }

    private static int CountStatements(string sql)
    {
        var count = 1;
        var quote = '\0';

        for (var i = 0; i < sql.Length; i++)
        {
            var c = sql[i];

            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c == ';' && i < sql.Length - 1)
            {
                count++;
            }
        }

        return count;
    }

    [GeneratedRegex(@"^(?:\s|--[^\n]*\n|/\*.*?\*/)*([A-Za-z]+)", RegexOptions.Singleline)]
    private static partial Regex VerbPattern();

    [GeneratedRegex(@"\b(?:FROM|JOIN|INTO|UPDATE)\s+(?:ONLY\s+)?(?:""(?<name>[^""]+)""|\[(?<name>[^\]]+)\]|(?<name>[A-Za-z_][A-Za-z0-9_$]*))", RegexOptions.IgnoreCase)]
    private static partial Regex TablePattern();
}
