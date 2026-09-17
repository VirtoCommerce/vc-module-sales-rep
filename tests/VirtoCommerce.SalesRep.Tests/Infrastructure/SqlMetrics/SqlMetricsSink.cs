using System;
using System.Collections.Concurrent;
using System.Threading;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure.SqlMetrics;

/// <summary>
/// Collects every recorded command for the whole run and writes the report when the process ends. xunit runs
/// tests in parallel, so the store is concurrent and each record carries the test it belongs to.
/// </summary>
internal static class SqlMetricsSink
{
    private static readonly ConcurrentQueue<SqlCommandRecord> _records = new();

    private static int _armed;

    public static bool Enabled => SqlMetricsOptions.Current.Enabled;

    public static void Add(SqlCommandRecord record)
    {
        if (!Enabled)
        {
            return;
        }

        Arm();
        _records.Enqueue(record);
    }

    /// <summary>
    /// Writes the report on the first recorded command rather than at load, so a run that records nothing
    /// leaves the previous report alone instead of overwriting it with an empty one.
    /// </summary>
    private static void Arm()
    {
        if (Interlocked.Exchange(ref _armed, 1) != 0)
        {
            return;
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => SqlMetricsReport.Write(_records, SqlMetricsOptions.Current);
    }
}
