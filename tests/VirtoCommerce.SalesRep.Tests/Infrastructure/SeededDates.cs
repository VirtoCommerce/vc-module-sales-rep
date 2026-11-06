using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

/// <summary>
/// Seeding rows from the past. The platform's audit trigger (<see cref="PlatformDbTriggers"/>) stamps every inserted
/// row with the current time, whatever date it carries, as a save on a running platform does: an order seeded "in
/// January" through a DbContext would be one placed today. So the rows are saved, then given the dates the test means
/// with a direct UPDATE, which no trigger re-stamps — as time passing would.
/// </summary>
internal static class SeededDates
{
    /// <summary>Saves the new rows, then gives each one that carries a CreatedDate that date (and its ModifiedDate, or the same).</summary>
    public static int SaveChangesKeepingDates(this DbContext db)
    {
        var dated = Capture(db);
        var result = db.SaveChanges();

        foreach (var row in dated)
        {
            db.Database.ExecuteSqlRaw(row.Sql, row.Parameters);
        }

        return result;
    }

    /// <summary>As <see cref="SaveChangesKeepingDates"/>.</summary>
    public static async Task<int> SaveChangesKeepingDatesAsync(this DbContext db)
    {
        var dated = Capture(db);
        var result = await db.SaveChangesAsync();

        foreach (var row in dated)
        {
            await db.Database.ExecuteSqlRawAsync(row.Sql, row.Parameters);
        }

        return result;
    }

    private static List<(string Sql, object[] Parameters)> Capture(DbContext db)
    {
        return db.ChangeTracker.Entries<IAuditable>()
            .Where(x => x.State == EntityState.Added && x.Entity.CreatedDate != default)
            .Select(ToUpdate)
            .ToList();
    }

    // Built from the entity's mapping (its table, its columns), so a TPH row updates its base table.
    private static (string Sql, object[] Parameters) ToUpdate(EntityEntry<IAuditable> entry)
    {
        var type = entry.Metadata;
        var key = type.FindPrimaryKey()!.Properties.Single();
        var table = type.GetTableName();

        var sql = "UPDATE \"" + table + "\" SET \"" + type.FindProperty(nameof(IAuditable.CreatedDate))!.GetColumnName() +
                  "\" = @created, \"" + type.FindProperty(nameof(IAuditable.ModifiedDate))!.GetColumnName() +
                  "\" = @modified WHERE \"" + key.GetColumnName() + "\" = @key";

        return (sql,
        [
            new SqliteParameter("@created", entry.Entity.CreatedDate),
            new SqliteParameter("@modified", entry.Entity.ModifiedDate ?? entry.Entity.CreatedDate),
            new SqliteParameter("@key", entry.Property(key.Name).CurrentValue),
        ]);
    }
}
