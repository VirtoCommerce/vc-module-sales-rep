using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
        var builder = new DbContextOptionsBuilder<TContext>().UseSqlite(connection);
        configure?.Invoke(builder);
        var options = builder.Options;

        using var context = (TContext)Activator.CreateInstance(typeof(TContext), options)!;
        context.Database.EnsureCreated();

        return options;
    }
}
