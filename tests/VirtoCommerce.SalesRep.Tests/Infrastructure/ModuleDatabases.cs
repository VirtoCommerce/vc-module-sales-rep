using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

/// <summary>
/// The in-memory SQLite databases of the platform and its modules, one per DbContext, as each module has its own.
/// A database is created when its module first uses it, with the schema of the module's EF model
/// (<c>EnsureCreated</c>), so a test pays only for the modules it reaches. Every command they run goes to the
/// <see cref="SqlCommandLog"/>.
/// <para>
/// Each DbContext reaches its database through a connection of its own, as on a database server. A module's code may
/// query on several DbContexts at once (the cart statistics load their periods in parallel, each on its own
/// repository), and one <see cref="SqliteConnection"/> used from two threads corrupts: the suite failed now and then
/// with a SQLite error, and the shared connection's <c>Close</c> threw. So a database is a named in-memory one in
/// shared-cache mode: SQLite's table locks keep its connections consistent, and Microsoft.Data.Sqlite waits out a
/// locked table until the command timeout. A keeper connection holds it alive until the provider that created this
/// instance disposes it; pooling is off, so no pooled connection outlives it.
/// </para>
/// </summary>
internal sealed class ModuleDatabases(SqlCommandLog commandLog) : IDisposable
{
    private readonly Lock _lock = new();
    private readonly List<SqliteConnection> _keepers = [];

    /// <summary>
    /// A new database for <typeparamref name="TContext"/>, with its schema. <paramref name="configure"/> adapts the
    /// model to SQLite where a real provider needs no help (a column type SQLite can't compare, an identity it can't
    /// generate), before the schema is created from it.
    /// </summary>
    public DbContextOptions<TContext> Create<TContext>(Action<DbContextOptionsBuilder> configure = null) where TContext : DbContext
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"{typeof(TContext).Name}-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
        }.ToString();

        var keeper = new SqliteConnection(connectionString);
        keeper.Open();

        lock (_lock)
        {
            _keepers.Add(keeper);
        }

        return SqliteTestDbContextFactory.CreateOptions<TContext>(connectionString, options =>
        {
            options.AddInterceptors(commandLog);
            configure?.Invoke(options);
        });
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var keeper in _keepers)
            {
                keeper.Dispose();
            }

            _keepers.Clear();
        }
    }
}
