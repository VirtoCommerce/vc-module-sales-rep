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
        return EnsureCreated(new DbContextOptionsBuilder<TContext>().UseSqlite(connection), configure);
    }

    /// <summary>
    /// As the overload above, but each context opens a connection of its own from <paramref name="connectionString"/>
    /// (a shared-cache in-memory database, which a connection the caller keeps open holds alive).
    /// </summary>
    public static DbContextOptions<TContext> CreateOptions<TContext>(string connectionString, Action<DbContextOptionsBuilder> configure = null)
        where TContext : DbContext
    {
        return EnsureCreated(new DbContextOptionsBuilder<TContext>().UseSqlite(connectionString), configure);
    }

    private static DbContextOptions<TContext> EnsureCreated<TContext>(DbContextOptionsBuilder<TContext> builder, Action<DbContextOptionsBuilder> configure)
        where TContext : DbContext
    {
        configure?.Invoke(builder);
        var options = builder.Options;

        using var context = (TContext)Activator.CreateInstance(typeof(TContext), options)!;
        context.Database.EnsureCreated();

        return options;
    }
}
