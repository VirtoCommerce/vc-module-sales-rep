using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.CustomerModule.Data.Model;
using VirtoCommerce.CustomerModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security.Repositories;
using VirtoCommerce.SalesRep.Tests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

/// <summary>
/// Spike: proves the two DbContexts this module composes (platform security + customer) materialize on
/// in-memory SQLite via EnsureCreated, and that a basic write/read round-trips. If this is green, the full
/// component-test harness is viable.
/// </summary>
[Trait("Category", "IntegrationSpike")]
public class SqliteSchemaSpikeTests
{
    [Fact]
    public void SecurityDbContext_EnsureCreated_And_RoundTripUser()
    {
        using var connection = SqliteTestDbContextFactory.CreateConnection();
        var options = SqliteTestDbContextFactory.CreateOptions<SecurityDbContext>(connection);

        using (var ctx = new SecurityDbContext(options))
        {
            ctx.Set<ApplicationUser>().Add(new ApplicationUser
            {
                Id = "u1",
                UserName = "rep@test.com",
                Email = "rep@test.com",
                MemberId = "m1",
            });
            ctx.SaveChanges();
        }

        using (var ctx = new SecurityDbContext(options))
        {
            var user = ctx.Set<ApplicationUser>().Single();
            user.MemberId.Should().Be("m1");
        }
    }

    [Fact]
    public async Task CustomerDbContext_EnsureCreated_TablesExist()
    {
        using var connection = SqliteTestDbContextFactory.CreateConnection();
        var options = SqliteTestDbContextFactory.CreateOptions<CustomerDbContext>(connection);

        await using var ctx = new CustomerDbContext(options);

        // Tables materialized from the model → queries succeed (the real round-trip goes through the
        // customer services in the full harness).
        (await ctx.Set<ContactEntity>().CountAsync()).Should().Be(0);
        (await ctx.Set<OrganizationMembershipEntity>().CountAsync()).Should().Be(0);
    }
}
