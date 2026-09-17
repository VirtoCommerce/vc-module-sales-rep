using System;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure.SqlMetrics;

/// <summary>
/// One executed command, as the report needs it: which test asked for it, what was sent, how long it took.
/// </summary>
internal sealed class SqlCommandRecord
{
    public string Test { get; init; }

    public string Sql { get; init; }

    public DateTime StartedUtc { get; init; }

    public TimeSpan Duration { get; init; }
}
