using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VirtoCommerce.SalesRep.Data.Repositories;

namespace VirtoCommerce.SalesRep.Data.SqlServer;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SalesRepDbContext>
{
    public SalesRepDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<SalesRepDbContext>();
        var connectionString = args.Length != 0 ? args[0] : "Server=(local);User=virto;Password=virto;Database=VirtoCommerce3;";

        builder.UseSqlServer(
            connectionString,
            options => options.MigrationsAssembly(typeof(SqlServerDataAssemblyMarker).Assembly.GetName().Name));

        return new SalesRepDbContext(builder.Options);
    }
}
