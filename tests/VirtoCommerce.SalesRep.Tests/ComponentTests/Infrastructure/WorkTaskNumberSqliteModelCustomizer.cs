using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using VirtoCommerce.TaskManagement.Data.Models;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

// WorkTaskEntity.Number is ValueGeneratedOnAdd() on a NON-KEY long, which real providers make an identity
// column. SQLite can only auto-generate an INTEGER PRIMARY KEY, so EF omits it from the INSERT and the NOT NULL
// constraint fires. Caller-supplied in the test database only; nothing under test reads it.
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
