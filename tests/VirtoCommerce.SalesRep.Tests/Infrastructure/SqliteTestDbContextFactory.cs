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

    /// <summary>Create options bound to <paramref name="connection"/> and materialize the schema for TContext.</summary>
    public static DbContextOptions<TContext> CreateOptions<TContext>(SqliteConnection connection)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(connection)
            .Options;

        using var context = (TContext)Activator.CreateInstance(typeof(TContext), options)!;
        context.Database.EnsureCreated();

        return options;
    }
}
