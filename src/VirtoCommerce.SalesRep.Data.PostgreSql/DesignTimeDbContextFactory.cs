using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VirtoCommerce.SalesRep.Data.Repositories;

namespace VirtoCommerce.SalesRep.Data.PostgreSql;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SalesRepDbContext>
{
    public SalesRepDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<SalesRepDbContext>();
        var connectionString = args.Length != 0 ? args[0] : "Server=localhost;Username=virto;Password=virto;Database=VirtoCommerce3;";

        builder.UseNpgsql(
            connectionString,
            options => options.MigrationsAssembly(typeof(PostgreSqlDataAssemblyMarker).Assembly.GetName().Name));

        return new SalesRepDbContext(builder.Options);
    }
}
