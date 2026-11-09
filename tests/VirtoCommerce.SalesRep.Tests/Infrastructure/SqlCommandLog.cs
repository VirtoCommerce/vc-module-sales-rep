using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

/// <summary>
/// The SQL every module database runs (<see cref="ModuleDatabases"/>), in order, with the DbContext that sent it: a
/// test clears it, acts, and counts the commands one module sent, where the services under test leave no other trace
/// of how often they called it.
/// </summary>
internal sealed class SqlCommandLog : DbCommandInterceptor
{
    private readonly ConcurrentQueue<(Type Context, string Text)> _commands = new();

    public void Clear() => _commands.Clear();

    /// <summary>The commands sent through <typeparamref name="TContext"/> since the last <see cref="Clear"/>.</summary>
    public IList<string> Of<TContext>() where TContext : DbContext
        => _commands.Where(x => x.Context == typeof(TContext)).Select(x => x.Text).ToList();

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Record(command, eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Record(command, eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Record(command, eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Record(command, eventData);
        return ValueTask.FromResult(result);
    }

    private void Record(DbCommand command, CommandEventData eventData)
    {
        _commands.Enqueue((eventData.Context?.GetType(), command.CommandText));
    }
}
