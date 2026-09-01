using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using VirtoCommerce.TaskManagement.Data.Models;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// vc-module-task-management maps <c>WorkTaskEntity.Number</c> as <c>ValueGeneratedOnAdd()</c> on a NON-KEY long,
/// which real providers turn into an identity column. SQLite can only auto-generate an INTEGER PRIMARY KEY, so EF
/// omits the column from the INSERT and the NOT NULL constraint fires. This customizer makes the number
/// caller-supplied in the component-test database only; nothing under test reads it.
/// </summary>
internal sealed class WorkTaskNumberSqliteModelCustomizer : RelationalModelCustomizer
{
    public WorkTaskNumberSqliteModelCustomizer(ModelCustomizerDependencies dependencies)
        : base(dependencies)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        modelBuilder.Entity<WorkTaskEntity>().Property(x => x.Number).ValueGeneratedNever();
    }
}
