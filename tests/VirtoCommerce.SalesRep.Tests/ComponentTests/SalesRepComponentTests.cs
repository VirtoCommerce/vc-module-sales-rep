using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Data.Model;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// End-to-end component tests: act through the real <c>SalesRepController</c> against real services on
/// in-memory SQLite, and assert against the databases + the returned aggregate (which GetByIdAsync reads
/// back from the DB). No mocks.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepComponentTests
{
    private static SalesRepOrganization Org(string id) => new() { OrganizationId = id };

    private static SalesRepDetails SimpleRep(string email, params string[] orgIds) => new()
    {
        Emails = [email],
        Password = "P@ssw0rd123!",
        FirstName = "Jane",
        LastName = "Rep",
        Organizations = orgIds.Select(Org).ToList(),
    };

    private static Address Addr(string line1, string city) => new()
    {
        AddressType = VirtoCommerce.CoreModule.Core.Common.AddressType.BillingAndShipping,
        FirstName = "Jane",
        LastName = "Rep",
        Line1 = line1,
        Line2 = "Suite 5",
        City = city,
        RegionId = "US-CA",
        RegionName = "California",
        PostalCode = "90001",
        CountryCode = "USA",
        CountryName = "United States",
        Phone = "+1-555-0100",
        Email = "addr@test.com",
    };

    [Fact]
    public async Task Create_WithAllFieldsPopulated_PersistsEveryField()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");

        var details = new SalesRepDetails
        {
            Salutation = "Ms.",
            FirstName = "Jane",
            MiddleName = "Q",
            LastName = "Rep",
            BirthDate = new DateTime(1990, 5, 15),
            TimeZone = "Pacific Standard Time",
            DefaultLanguage = "en-US",
            CurrencyCode = "USD",
            About = "Senior sales rep",
            PhotoUrl = "https://example.com/jane.png",
            Status = "Active",
            StoreId = "B2B-store",
            Password = "P@ssw0rd123!",
            IsLocked = true,
            Emails = ["jane@test.com", "jane.alt@test.com"],
            Phones = ["+1-555-1000", "+1-555-2000"],
            Addresses = [Addr("100 Main St", "Los Angeles"), Addr("200 Oak Ave", "San Diego")],
            Organizations = [Org("org-1"), Org("org-2")],
        };

        var created = SalesRepTestContext.Unwrap(await ctx.Controller.Create(details));

        // Scalars round-trip
        created.Id.Should().NotBeNullOrEmpty();
        created.Salutation.Should().Be("Ms.");
        created.FirstName.Should().Be("Jane");
        created.MiddleName.Should().Be("Q");
        created.LastName.Should().Be("Rep");
        created.FullName.Should().Be("Jane Q Rep");
        created.BirthDate!.Value.Date.Should().Be(new DateTime(1990, 5, 15)); // stored date (VC normalizes DateTime to UTC)
        created.TimeZone.Should().Be("Pacific Standard Time");
        created.DefaultLanguage.Should().Be("en-US");
        created.CurrencyCode.Should().Be("USD");
        created.About.Should().Be("Senior sales rep");
        created.PhotoUrl.Should().Be("https://example.com/jane.png");
        created.Status.Should().Be("Active");
        created.StoreId.Should().Be("B2B-store");
        created.IsLocked.Should().BeTrue();
        created.HasGlobalSalesRepRole.Should().BeTrue();
        created.RoleId.Should().NotBeNullOrEmpty();
        created.Password.Should().BeNull("password is write-only and never serialized back");

        // Lists round-trip
        created.Emails.Should().ContainInOrder("jane@test.com", "jane.alt@test.com");
        created.Emails[0].Should().Be("jane@test.com", "the login email is first");
        created.Phones.Should().BeEquivalentTo(["+1-555-1000", "+1-555-2000"]);
        created.Organizations.Select(o => o.OrganizationId).Should().BeEquivalentTo(["org-1", "org-2"]);

        created.Addresses.Should().HaveCount(2);
        var la = created.Addresses.Single(a => a.City == "Los Angeles");
        la.Line1.Should().Be("100 Main St");
        la.Line2.Should().Be("Suite 5");
        la.PostalCode.Should().Be("90001");
        la.RegionName.Should().Be("California");
        la.CountryCode.Should().Be("USA");
        la.AddressType.Should().Be(VirtoCommerce.CoreModule.Core.Common.AddressType.BillingAndShipping);

        // DB: account carries StoreId + login + lockout
        await using (var sdb = ctx.NewSecurityDbContext())
        {
            var user = await sdb.Set<ApplicationUser>().SingleAsync(x => x.MemberId == created.Id);
            user.UserName.Should().Be("jane@test.com");
            user.StoreId.Should().Be("B2B-store");
            (user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow).Should().BeTrue();
        }

        // DB: contact, addresses and per-org memberships persisted
        await using (var cdb = ctx.NewCustomerDbContext())
        {
            (await cdb.Set<ContactEntity>().SingleAsync(x => x.Id == created.Id)).Name.Should().Be("Jane Q Rep");
            (await cdb.Set<AddressEntity>().CountAsync(x => x.MemberId == created.Id)).Should().Be(2);
            (await cdb.Set<OrganizationMembershipEntity>().CountAsync(x => x.UserId == created.UserId)).Should().Be(2);
        }
    }

    [Fact]
    public async Task Create_WithoutExplicitStatus_InheritsStoreDefaultContactStatus()
    {
        using var ctx = SalesRepTestContext.Create();
        // The store's ContactDefaultStatus is what a self-registered contact would get (Approved => Active in the storefront).
        ctx.SetStoreContactDefaultStatus("B2B-store", "Approved");
        await ctx.SeedOrganizationsAsync("org-1");

        // CreateRepInStoreAsync does NOT set a Status, so the store default must be applied.
        var created = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", "B2B-store", "org-1");

        created.Status.Should().Be("Approved", "a rep with no explicit status inherits the store's ContactDefaultStatus");
    }

    [Fact]
    public async Task Create_WithoutStoreOrStatus_LeavesStatusUnset()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");

        // No store bound (storeId null) and no explicit status => nothing to seed the status from.
        var created = await ctx.CreateRepAsync("Nostore", "Rep", "nostore@test.com", "org-1");

        created.Status.Should().BeNull("with no store bound there is no default contact status to apply");
    }

    [Fact]
    public async Task Update_ComplexListAndScalarChanges_AppliedPrecisely()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-a", "org-b", "org-c");

        var created = SalesRepTestContext.Unwrap(await ctx.Controller.Create(new SalesRepDetails
        {
            FirstName = "Jane",
            LastName = "Rep",
            About = "Original bio",
            Password = "P@ssw0rd123!",
            Emails = ["login@test.com", "extra1@test.com"],
            Phones = ["+1-111", "+1-222"],
            Addresses = [Addr("100 Main St", "Los Angeles"), Addr("200 Oak Ave", "San Diego")],
            Organizations = [Org("org-a"), Org("org-b")],
        }));

        // Build the edit off the returned aggregate (retains address Keys — like the blade round-trip).
        var edit = created;
        edit.LastName = "Renamed";
        edit.About = "Updated bio";
        edit.TimeZone = "UTC";

        // Emails: keep the login, swap the additional email.
        edit.Emails = ["login@test.com", "extra2@test.com"];
        // Phones: drop one.
        edit.Phones = ["+1-111"];

        // Addresses: edit the LA address in place (keep its Key), remove San Diego, add a new one.
        var la = edit.Addresses.Single(a => a.City == "Los Angeles");
        la.City = "Sacramento";
        la.Line1 = "999 Capitol Mall";
        edit.Addresses = [la, Addr("300 Pine Rd", "Fresno")];

        // Organizations: remove org-a, keep org-b, add org-c.
        edit.Organizations = [Org("org-b"), Org("org-c")];

        var updated = SalesRepTestContext.Unwrap(await ctx.Controller.Update(edit));

        // Scalars
        updated.FullName.Should().Be("Jane Renamed");
        updated.About.Should().Be("Updated bio");
        updated.TimeZone.Should().Be("UTC");

        // Emails / phones
        updated.Emails.Should().ContainInOrder("login@test.com", "extra2@test.com");
        updated.Emails.Should().NotContain("extra1@test.com");
        updated.Phones.Should().BeEquivalentTo(["+1-111"]);

        // Addresses: edited (LA -> Sacramento), removed (San Diego), added (Fresno)
        updated.Addresses.Select(a => a.City).Should().BeEquivalentTo(["Sacramento", "Fresno"]);
        updated.Addresses.Single(a => a.City == "Sacramento").Line1.Should().Be("999 Capitol Mall");
        updated.Addresses.Should().NotContain(a => a.City == "San Diego");

        // Organizations
        updated.Organizations.Select(o => o.OrganizationId).Should().BeEquivalentTo(["org-b", "org-c"]);

        // DB reflects the final state
        await using (var cdb = ctx.NewCustomerDbContext())
        {
            (await cdb.Set<AddressEntity>().CountAsync(x => x.MemberId == created.Id)).Should().Be(2);
            var orgIds = await cdb.Set<OrganizationMembershipEntity>()
                .Where(x => x.UserId == created.UserId)
                .Select(x => x.OrganizationId)
                .ToListAsync();
            orgIds.Should().BeEquivalentTo(["org-b", "org-c"]);
        }

        // Login email unchanged -> account UserName unchanged
        await using (var sdb = ctx.NewSecurityDbContext())
        {
            (await sdb.Set<ApplicationUser>().SingleAsync(x => x.MemberId == created.Id)).UserName.Should().Be("login@test.com");
        }
    }

    [Fact]
    public async Task Update_ChangingLoginEmail_SyncsAccountUserName()
    {
        using var ctx = SalesRepTestContext.Create();
        var created = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("old-login@test.com")));

        created.Emails = ["new-login@test.com"];
        var updated = SalesRepTestContext.Unwrap(await ctx.Controller.Update(created));

        updated.Emails[0].Should().Be("new-login@test.com");

        await using var sdb = ctx.NewSecurityDbContext();
        var user = await sdb.Set<ApplicationUser>().SingleAsync(x => x.MemberId == created.Id);
        user.UserName.Should().Be("new-login@test.com");
        user.Email.Should().Be("new-login@test.com");
    }

    [Fact]
    public async Task Delete_CascadesToAccountAndMemberships_ViaRealEventPath()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-9");
        var created = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("del@test.com", "org-9")));

        await using (var db = ctx.NewCustomerDbContext())
        {
            (await db.Set<OrganizationMembershipEntity>().CountAsync(x => x.OrganizationId == "org-9")).Should().Be(1);
        }

        await ctx.Controller.Delete([created.Id]);

        await using (var db = ctx.NewCustomerDbContext())
        {
            (await db.Set<ContactEntity>().CountAsync(x => x.Id == created.Id)).Should().Be(0);
            (await db.Set<OrganizationMembershipEntity>().CountAsync(x => x.OrganizationId == "org-9")).Should().Be(0);
        }

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
        var withOrg = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("assigned@test.com", "org-x")));
        var noOrg = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("unassigned@test.com")));

        var all = SalesRepTestContext.Unwrap(await ctx.Controller.Search(new SalesRepSearchCriteria { Take = 100 }));
        all.Results.Select(r => r.Id).Should().Contain([withOrg.Id, noOrg.Id]);

        var unassignedOnly = SalesRepTestContext.Unwrap(await ctx.Controller.Search(new SalesRepSearchCriteria { OnlyUnassigned = true, Take = 100 }));
        unassignedOnly.Results.Select(r => r.Id).Should().Contain(noOrg.Id).And.NotContain(withOrg.Id);
    }
}
