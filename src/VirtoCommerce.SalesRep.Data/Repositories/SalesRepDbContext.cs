using System.Reflection;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Data.Extensions;
using VirtoCommerce.Platform.Data.Infrastructure;
using VirtoCommerce.SalesRep.Data.Models;

namespace VirtoCommerce.SalesRep.Data.Repositories;

public class SalesRepDbContext : DbContextBase
{
    public SalesRepDbContext(DbContextOptions<SalesRepDbContext> options)
        : base(options)
    {
    }

    protected SalesRepDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DocumentMetadataEntity>().ToAuditableEntityTable("SalesRepDocumentMetadata");

        switch (Database.ProviderName)
        {
            case "Pomelo.EntityFrameworkCore.MySql":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.SalesRep.Data.MySql"));
                break;
            case "Npgsql.EntityFrameworkCore.PostgreSQL":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.SalesRep.Data.PostgreSql"));
                break;
            case "Microsoft.EntityFrameworkCore.SqlServer":
                modelBuilder.ApplyConfigurationsFromAssembly(Assembly.Load("VirtoCommerce.SalesRep.Data.SqlServer"));
                break;
        }
    }
}
