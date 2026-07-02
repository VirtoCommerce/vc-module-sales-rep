using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.CustomerModule.Data.Model;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// End-to-end component tests: seed nothing (start empty), act through the real <c>SalesRepController</c>
/// against real services on in-memory SQLite, and assert against the databases. No mocks.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepComponentTests
{
    private static SalesRepDetails NewRep(string email, params string[] orgIds) => new()
    {
        Emails = [email],
        Password = "P@ssw0rd123!",
        FirstName = "Jane",
        LastName = "Rep",
        Organizations = orgIds.Select(id => new SalesRepOrganization { OrganizationId = id }).ToList(),
    };

    [Fact]
    public async Task Create_PersistsContact_Account_Role_AndMembership()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");

        var created = SalesRepTestContext.Unwrap(await ctx.Controller.Create(NewRep("rep@test.com", "org-1")));

        // Returned aggregate (read back from the DB by GetByIdAsync) reflects the persisted state.
        created.Id.Should().NotBeNullOrEmpty();
        created.FullName.Should().Be("Jane Rep");
        created.HasGlobalSalesRepRole.Should().BeTrue();
        created.RoleId.Should().NotBeNullOrEmpty();
        created.Organizations.Should().ContainSingle(o => o.OrganizationId == "org-1");

        // Contact (member) row
        await using (var db = ctx.NewCustomerDbContext())
        {
            var contact = await db.Set<ContactEntity>().SingleOrDefaultAsync(x => x.Id == created.Id);
            contact.Should().NotBeNull();
            contact!.Name.Should().Be("Jane Rep");
        }

        // Login account row, linked to the member
        await using (var db = ctx.NewSecurityDbContext())
        {
            var user = await db.Set<ApplicationUser>().SingleOrDefaultAsync(x => x.MemberId == created.Id);
            user.Should().NotBeNull();
            user!.UserName.Should().Be("rep@test.com");
        }

        // Per-org membership row for the served org
        await using (var db = ctx.NewCustomerDbContext())
        {
            (await db.Set<OrganizationMembershipEntity>().CountAsync(x => x.OrganizationId == "org-1")).Should().Be(1);
        }
    }

    [Fact]
    public async Task Update_ChangesProfileAndRepointsOrganizations()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-a", "org-b");
        var created = SalesRepTestContext.Unwrap(await ctx.Controller.Create(NewRep("edit@test.com", "org-a")));

        // Edit: rename + swap served org a -> b
        created.LastName = "Renamed";
        created.Organizations = [new SalesRepOrganization { OrganizationId = "org-b" }];
        var updated = SalesRepTestContext.Unwrap(await ctx.Controller.Update(created));

        updated.FullName.Should().Be("Jane Renamed");
        updated.Organizations.Should().ContainSingle(o => o.OrganizationId == "org-b");

        await using var db = ctx.NewCustomerDbContext();
        (await db.Set<ContactEntity>().SingleAsync(x => x.Id == created.Id)).Name.Should().Be("Jane Renamed");
        // Old org membership revoked, new one granted.
        (await db.Set<OrganizationMembershipEntity>().CountAsync(x => x.OrganizationId == "org-a")).Should().Be(0);
        (await db.Set<OrganizationMembershipEntity>().CountAsync(x => x.OrganizationId == "org-b")).Should().Be(1);
    }

    [Fact]
    public async Task Delete_CascadesToAccountAndMemberships_ViaRealEventPath()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-9");
        var created = SalesRepTestContext.Unwrap(await ctx.Controller.Create(NewRep("del@test.com", "org-9")));

        await using (var db = ctx.NewCustomerDbContext())
        {
            (await db.Set<OrganizationMembershipEntity>().CountAsync(x => x.OrganizationId == "org-9")).Should().Be(1);
        }

        await ctx.Controller.Delete([created.Id]);

        // Contact + membership gone (membership cleared by the UserChangedEvent -> customer handler cascade).
        await using (var db = ctx.NewCustomerDbContext())
        {
            (await db.Set<ContactEntity>().CountAsync(x => x.Id == created.Id)).Should().Be(0);
            (await db.Set<OrganizationMembershipEntity>().CountAsync(x => x.OrganizationId == "org-9")).Should().Be(0);
        }

        // Account gone.
        await using (var db = ctx.NewSecurityDbContext())
        {
            (await db.Set<ApplicationUser>().CountAsync(x => x.MemberId == created.Id)).Should().Be(0);
        }
    }

    [Fact]
    public async Task Search_ReturnsCreatedReps_AndFiltersUnassigned()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-x");
        var withOrg = SalesRepTestContext.Unwrap(await ctx.Controller.Create(NewRep("assigned@test.com", "org-x")));
        var noOrg = SalesRepTestContext.Unwrap(await ctx.Controller.Create(NewRep("unassigned@test.com")));

        var all = SalesRepTestContext.Unwrap(await ctx.Controller.Search(new SalesRepSearchCriteria { Take = 100 }));
        all.Results.Select(r => r.Id).Should().Contain([withOrg.Id, noOrg.Id]);

        var unassignedOnly = SalesRepTestContext.Unwrap(await ctx.Controller.Search(new SalesRepSearchCriteria { OnlyUnassigned = true, Take = 100 }));
        unassignedOnly.Results.Select(r => r.Id).Should().Contain(noOrg.Id).And.NotContain(withOrg.Id);
    }
}
