using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.CustomerModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Security.Repositories;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Tests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// Validates the customer slice: real MemberService and OrganizationMembership services persist and read back
/// through SQLite, and the full service graph (including the RAM Lucene member-search chain) constructs.
/// </summary>
[Trait("Category", "Component")]
public class CustomerSliceTests
{
    [Fact]
    public async Task RealMemberService_SavesAndReadsContact()
    {
        using var connections = new SqliteConnectionScope();
        var services = new ServiceCollection()
            .AddSecuritySlice(connections.SecurityOptions)
            .AddCustomerSlice(connections.CustomerOptions);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var memberService = scope.ServiceProvider.GetRequiredService<IMemberService>();

        var contact = AbstractTypeFactory<Contact>.TryCreateInstance();
        contact.Id = "contact-1";
        contact.FirstName = "Jane";
        contact.LastName = "Rep";
        contact.Name = "Jane Rep";
        await memberService.SaveChangesAsync([contact]);

        var loaded = (await memberService.GetByIdsAsync(["contact-1"], MemberResponseGroup.Full.ToString()))
            .OfType<Contact>()
            .Single();
        loaded.Name.Should().Be("Jane Rep");
        loaded.LastName.Should().Be("Rep");
    }

    [Fact]
    public async Task RealMembershipServices_SaveAndSearch()
    {
        using var connections = new SqliteConnectionScope();
        var services = new ServiceCollection()
            .AddSecuritySlice(connections.SecurityOptions)
            .AddCustomerSlice(connections.CustomerOptions);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var membershipService = scope.ServiceProvider.GetRequiredService<IOrganizationMembershipService>();
        var membershipSearch = scope.ServiceProvider.GetRequiredService<IOrganizationMembershipSearchService>();

        var membership = AbstractTypeFactory<OrganizationMembership>.TryCreateInstance();
        membership.UserId = "user-1";
        membership.OrganizationId = "org-1";
        await membershipService.SaveChangesAsync([membership]);

        var result = await membershipSearch.SearchAsync(new OrganizationMembershipSearchCriteria { UserId = "user-1" });
        result.Results.Should().ContainSingle(m => m.OrganizationId == "org-1");

        // Proves the whole graph (incl. RAM Lucene member-search chain) resolves.
        scope.ServiceProvider.GetRequiredService<IMemberSearchService>().Should().NotBeNull();
    }

    /// <summary>Owns the per-test SQLite connections + materialized options for both contexts.</summary>
    private sealed class SqliteConnectionScope : System.IDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection _securityConnection = SqliteTestDbContextFactory.CreateConnection();
        private readonly Microsoft.Data.Sqlite.SqliteConnection _customerConnection = SqliteTestDbContextFactory.CreateConnection();

        public Microsoft.EntityFrameworkCore.DbContextOptions<SecurityDbContext> SecurityOptions { get; }
        public Microsoft.EntityFrameworkCore.DbContextOptions<CustomerDbContext> CustomerOptions { get; }

        public SqliteConnectionScope()
        {
            SecurityOptions = SqliteTestDbContextFactory.CreateOptions<SecurityDbContext>(_securityConnection);
            CustomerOptions = SqliteTestDbContextFactory.CreateOptions<CustomerDbContext>(_customerConnection);
        }

        public void Dispose()
        {
            _securityConnection.Dispose();
            _customerConnection.Dispose();
        }
    }
}
