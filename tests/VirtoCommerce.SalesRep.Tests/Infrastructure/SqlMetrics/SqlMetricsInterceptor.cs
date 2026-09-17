using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure.SqlMetrics;

/// <summary>
/// Records every command EF executes, whichever context and whichever provider. Attached by
/// <see cref="SqliteTestDbContextFactory"/> only when <c>sqlmetrics.json</c> turns it on.
/// </summary>
/// <remarks>
/// IMPORTANT (keep): the test is captured once, when the context is built, and never read again. Asking xunit
/// which test is current at the moment a command runs attributes work to whoever happens to be running when it
/// lands. Measured over two parallel runs of the unchanged suite: reading it per command, the counts moved by
/// 163 queries while the total changed by 13 — 8% of the movement was real and the rest was work landing on the
/// wrong test. Capturing it here, 42% is real. A context belongs to the test that built it, so its commands do.
/// </remarks>
internal sealed class SqlMetricsInterceptor : DbCommandInterceptor
{
    private readonly string _test;

    public SqlMetricsInterceptor(string test)
    {
        _test = test;
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Record(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Record(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object result)
    {
        Record(command, eventData);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object result, CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    private void Record(DbCommand command, CommandExecutedEventData eventData)
    {
        SqlMetricsSink.Add(new SqlCommandRecord
        {
            Test = _test,
            Sql = command.CommandText,
            StartedUtc = eventData.StartTime.UtcDateTime,
            Duration = eventData.Duration,
        });
    }
}
