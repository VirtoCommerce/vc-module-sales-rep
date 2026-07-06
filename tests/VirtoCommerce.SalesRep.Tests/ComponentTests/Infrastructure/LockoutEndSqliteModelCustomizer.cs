using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// EF Core's SQLite provider can't translate comparisons on <see cref="DateTimeOffset"/> (which is how
/// ASP.NET Identity models <c>ApplicationUser.LockoutEnd</c>). This model customizer stores LockoutEnd as a UTC
/// <see cref="DateTime"/> in the component-test database, so <c>UserSearchCriteria.OnlyUnlocked</c>
/// (<c>LockoutEnd &lt;= now</c>) translates to SQL. It is a test-harness-only conversion — real databases
/// (SQL Server / PostgreSQL) handle DateTimeOffset natively and don't need it.
/// </summary>
internal sealed class LockoutEndSqliteModelCustomizer : RelationalModelCustomizer
{
    private static readonly ValueConverter<DateTimeOffset, DateTime> LockoutEndConverter = new(
        offset => offset.UtcDateTime,
        utc => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));

    public LockoutEndSqliteModelCustomizer(ModelCustomizerDependencies dependencies)
        : base(dependencies)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        modelBuilder.Entity<ApplicationUser>()
            .Property(user => user.LockoutEnd)
            .HasConversion(LockoutEndConverter);
    }
}
