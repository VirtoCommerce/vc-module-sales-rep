using System;
using System.IO;
using System.Text.Json;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure.SqlMetrics;

/// <summary>
/// Settings for the SQL metrics run, read once from <c>sqlmetrics.json</c> next to the test assembly.
/// Absent or unreadable file means the whole feature stays off, so nothing is recorded and no report written.
/// </summary>
internal sealed class SqlMetricsOptions
{
    private const string FileName = "sqlmetrics.json";

    private static readonly Lazy<SqlMetricsOptions> _current = new(Load);

    public static SqlMetricsOptions Current => _current.Value;

    public bool Enabled { get; set; }

    public bool Html { get; set; } = true;

    public bool Counts { get; set; } = true;

    /// <summary>
    /// How many tests the page carries, heaviest first. The whole suite is ~20k queries, which makes a page of
    /// a dozen megabytes that no browser enjoys; the counts file keeps every test regardless. 0 means all.
    /// </summary>
    public int HtmlMaxTests { get; set; } = 50;

    /// <summary>
    /// Where the report goes. A relative path is resolved against the test assembly, so the default walks out
    /// of bin/&lt;configuration&gt;/&lt;tfm&gt; back to the test project.
    /// </summary>
    public string OutputDirectory { get; set; } = "../../../sql-metrics";

    public string ResolveOutputDirectory()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, OutputDirectory));
    }

    private static SqlMetricsOptions Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, FileName);

        if (!File.Exists(path))
        {
            return new SqlMetricsOptions();
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };

            return JsonSerializer.Deserialize<SqlMetricsOptions>(File.ReadAllText(path), options) ?? new SqlMetricsOptions();
        }
        catch (JsonException)
        {
            // A malformed settings file must not fail the suite it is only observing
            return new SqlMetricsOptions();
        }
    }
}
