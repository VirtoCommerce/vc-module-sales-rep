using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure.SqlMetrics;

/// <summary>
/// Writes the two artefacts of a run: a counts file meant to be committed and diffed between runs, and a page
/// that shows every query under the test that ran it.
/// </summary>
internal static class SqlMetricsReport
{
    private const string CountsFile = "sql-counts.txt";
    private const string HtmlFile = "sql-report.html";
    private const string RecordsPlaceholder = "/*__RECORDS__*/[]";
    private const string DashboardResource = "VirtoCommerce.SalesRep.Tests.Infrastructure.SqlMetrics.SqlMetricsDashboard.html";

    private static readonly JsonSerializerOptions _json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static void Write(IReadOnlyCollection<SqlCommandRecord> records, SqlMetricsOptions options)
    {
        if (records.Count == 0)
        {
            return;
        }

        var directory = options.ResolveOutputDirectory();
        Directory.CreateDirectory(directory);

        var tests = records
            .GroupBy(x => x.Test, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToList();

        if (options.Counts)
        {
            WriteCounts(Path.Combine(directory, CountsFile), tests);
        }

        if (options.Html)
        {
            var shown = options.HtmlMaxTests > 0
                ? tests.OrderByDescending(x => x.Count()).Take(options.HtmlMaxTests).OrderBy(x => x.Key, StringComparer.Ordinal).ToList()
                : tests;

            WriteHtml(Path.Combine(directory, HtmlFile), shown);
        }
    }

    /// <summary>
    /// One line per test, sorted, and nothing else — a total would turn every single change into two diff
    /// lines. Commit this file and the next run's diff is the list of tests whose query count moved.
    /// </summary>
    private static void WriteCounts(string path, IReadOnlyCollection<IGrouping<string, SqlCommandRecord>> tests)
    {
        var text = new StringBuilder();

        foreach (var test in tests)
        {
            text.Append(test.Key).Append('\t').Append(test.Count().ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        File.WriteAllText(path, text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteHtml(string path, IReadOnlyCollection<IGrouping<string, SqlCommandRecord>> tests)
    {
        var dashboard = ReadDashboard();

        if (dashboard == null)
        {
            return;
        }

        var rows = new List<object>();
        var index = 0;

        foreach (var test in tests)
        {
            var commands = test.OrderBy(x => x.StartedUtc).ToList();
            var operation = Id(index++, 32);
            var owner = Id(index++, 16);

            var start = commands.Min(x => x.StartedUtc);
            var end = commands.Max(x => x.StartedUtc + x.Duration);
            var (name, container) = SplitTestName(test.Key);

            rows.Add(new
            {
                time = Moment(start),
                source = "tests",
                type = "RequestData",
                duration = Length(end - start),
                operation,
                parent = string.Empty,
                id = owner,
                name,
                address = container,
            });

            foreach (var command in commands)
            {
                rows.Add(new
                {
                    time = Moment(command.StartedUtc),
                    source = "tests",
                    type = "RemoteDependencyData",
                    duration = Length(command.Duration),
                    operation,
                    parent = owner,
                    id = Id(index++, 16),
                    name = SqlLabel.Describe(command.Sql),
                    address = command.Sql,
                });
            }
        }

        File.WriteAllText(path, dashboard.Replace(RecordsPlaceholder, JsonSerializer.Serialize(rows, _json), StringComparison.Ordinal));
    }

    private static string ReadDashboard()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(DashboardResource);

        if (stream == null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    /// <summary>
    /// "Namespace.Class.Method(arg: 1)" splits into the method with its arguments, which names the row, and the
    /// class, which the page shows beside it.
    /// </summary>
    private static (string Name, string Container) SplitTestName(string display)
    {
        var arguments = display.IndexOf('(', StringComparison.Ordinal);
        var head = arguments < 0 ? display : display[..arguments];
        var separator = head.LastIndexOf('.');

        return separator < 0
            ? (display, string.Empty)
            : (display[(separator + 1)..], head[..separator]);
    }

    private static string Moment(DateTime utc)
    {
        return utc.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    // The page reads a duration as hours:minutes:seconds.fraction, and wants the fraction even when it is zero
    private static string Length(TimeSpan value)
    {
        return (value < TimeSpan.Zero ? TimeSpan.Zero : value).ToString(@"hh\:mm\:ss\.fffffff", CultureInfo.InvariantCulture);
    }

    private static string Id(int value, int width)
    {
        return value.ToString("x", CultureInfo.InvariantCulture).PadLeft(width, '0');
    }
}
