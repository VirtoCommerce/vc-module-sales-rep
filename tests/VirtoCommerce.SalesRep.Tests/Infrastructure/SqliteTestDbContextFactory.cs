using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.SalesRep.Tests.Infrastructure.SqlMetrics;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

/// <summary>
/// Builds EF contexts over a shared in-memory SQLite database for component tests. The schema is created from
/// the current EF model via <see cref="DatabaseFacade.EnsureCreated"/> (migrations are NOT used), so the DB
/// reflects the model, not the migration history. The connection must stay open for the DB to live — the
/// caller owns and disposes it.
/// </summary>
public static class SqliteTestDbContextFactory
{
    public static SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Create options bound to <paramref name="connection"/> and materialize the schema for TContext.
    /// <paramref name="configure"/> can tweak the options (e.g. replace the model customizer) before the schema
    /// is created, so model changes are reflected in both the schema and queries.
    /// </summary>
    public static DbContextOptions<TContext> CreateOptions<TContext>(SqliteConnection connection, Action<DbContextOptionsBuilder> configure = null)
        where TContext : DbContext
    {
        var options = Build<TContext>(connection, configure, recorder: null);

        using var context = (TContext)Activator.CreateInstance(typeof(TContext), options)!;
        context.Database.EnsureCreated();

        // The context belongs to the test building it now, and so will every command it later runs. Asking
        // which test is current at execution time instead lands the work on whoever happens to be running.
        var owner = SqlMetricsSink.Enabled ? TestContext.Current?.Test?.TestDisplayName : null;

        // The schema was raised on options without the recorder, so the CREATE TABLE burst is not counted
        return string.IsNullOrEmpty(owner)
            ? options
            : Build<TContext>(connection, configure, new SqlMetricsInterceptor(owner));
    }

    private static DbContextOptions<TContext> Build<TContext>(SqliteConnection connection, Action<DbContextOptionsBuilder> configure, SqlMetricsInterceptor recorder)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>().UseSqlite(connection);
        configure?.Invoke(builder);

        if (recorder != null)
        {
            builder.AddInterceptors(recorder);
        }

        return builder.Options;
    }
}
