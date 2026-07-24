using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.CustomerModule.Data.Model;
using VirtoCommerce.Platform.Core.Common;
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
            BirthDate = new DateTime(1990, 5, 15, 0, 0, 0, DateTimeKind.Utc),
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
        created.BirthDate!.Value.Date.Should().Be(new DateTime(1990, 5, 15)); // UTC input avoids a local-midnight → prior-UTC-day shift in non-UTC timezones
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
    public async Task ChangeRole_RePointsGlobalAccountRole_NotOnlyMemberships()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var role2 = await ctx.CreateGrantingRoleAsync("Sales Representative 2");
        var role3 = await ctx.CreateGrantingRoleAsync("Sales Representative 3");

        var created = await ctx.CreateRepInStoreAsync("Jane", "Rep", "jane@test.com", "B2B-store", "org-1");
        var userId = await GetAccountIdAsync(ctx, created.Id);

        // No RoleId was sent, so the rep was created with whichever granting role the resolver enumerates
        // first — an unordered query over random GUID ids, i.e. a per-run coin flip between role2 and role3.
        // Pick the switch targets relative to it so BOTH edits below are guaranteed real role changes
        // (a same-role "switch" is a no-op that would mask the regression and made earlier repros flaky).
        var firstTarget = created.RoleId == role2.Id ? role3 : role2;
        var secondTarget = firstTarget == role2 ? role3 : role2;

        // COLD cache: the account read inside the edit is a guaranteed cache miss, so FindByIdAsync hands the
        // service an instance that is also EF-tracked by the updating manager's own DbContext. This is the
        // state in which the field bug reproduced: mutating that instance and passing it back to UpdateAsync
        // made the platform's role diff run against itself (LoadExistingUser resolves the SAME tracked object
        // and reloads its Roles from the DB), so the role change was silently lost — the global assignment
        // diverged from the per-org memberships, which are written directly and always re-pointed.
        SalesRepTestContext.ExpireSecurityCache();
        created.RoleId = firstTarget.Id;
        var afterFirst = SalesRepTestContext.Unwrap(await ctx.Controller.Update(created));

        (await GetGlobalRoleIdsAsync(ctx, userId)).Should().BeEquivalentTo([firstTarget.Id],
            "a cold-cache edit must re-point the global account role, not only the per-org memberships");

        // WARM cache: the account is served from the platform memory cache (an instance owned by a foreign,
        // already-disposed scope) — the other read path an edit can hit; must re-point all the same.
        await ctx.WarmUserCacheAsync(userId);
        afterFirst.RoleId = secondTarget.Id;
        SalesRepTestContext.Unwrap(await ctx.Controller.Update(afterFirst));

        (await GetGlobalRoleIdsAsync(ctx, userId)).Should().BeEquivalentTo([secondTarget.Id],
            "a warm-cache edit must re-point the global account role and drop the previous granting role");
    }

    private static async Task<string> GetAccountIdAsync(SalesRepTestContext ctx, string memberId)
    {
        await using var sdb = ctx.NewSecurityDbContext();
        var user = await sdb.Set<ApplicationUser>().SingleAsync(x => x.MemberId == memberId);
        return user.Id;
    }

    private static async Task<List<string>> GetGlobalRoleIdsAsync(SalesRepTestContext ctx, string userId)
    {
        await using var sdb = ctx.NewSecurityDbContext();
        return await sdb.Set<IdentityUserRole<string>>()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();
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

    // ---- create-failure compensation (rollback) ----

    [Fact]
    public async Task Create_WhenAccountCreationFails_RollsBackContact()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var first = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("dup@test.com", "org-1")));

        // A second rep with the same login email: the contact is saved first, then account creation fails on the
        // duplicate user name — the compensation path must remove the just-created contact so no orphan remains.
        var act = () => ctx.Controller.Create(SimpleRep("dup@test.com", "org-1"));
        await act.Should().ThrowAsync<InvalidOperationException>();

        await using (var cdb = ctx.NewCustomerDbContext())
        {
            (await cdb.Set<ContactEntity>().CountAsync()).Should().Be(1, "the failed create must not leave an orphan contact");
        }
        await using (var sdb = ctx.NewSecurityDbContext())
        {
            (await sdb.Set<ApplicationUser>().CountAsync()).Should().Be(1);
        }

        // The pre-existing rep is untouched.
        SalesRepTestContext.Unwrap(await ctx.Controller.Get(first.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Create_WithNonExistentOrganization_ThrowsAndLeavesNoOrphan()
    {
        using var ctx = SalesRepTestContext.Create();
        // "no-such-org" was never seeded — persisting the contact's org relation must fail, and the failed
        // create must leave neither a contact nor an account behind.
        var act = () => ctx.Controller.Create(SimpleRep("ghost-org@test.com", "no-such-org"));
        await act.Should().ThrowAsync<Exception>();

        await using (var cdb = ctx.NewCustomerDbContext())
        {
            (await cdb.Set<ContactEntity>().CountAsync()).Should().Be(0, "a create that failed on a non-existent organization must not leave an orphan contact");
            (await cdb.Set<OrganizationMembershipEntity>().CountAsync()).Should().Be(0);
        }
        await using (var sdb = ctx.NewSecurityDbContext())
        {
            (await sdb.Set<ApplicationUser>().CountAsync()).Should().Be(0);
        }
    }

    // ---- per-organization-only reps (no global role) — the other half of the UNION ----

    [Fact]
    public async Task Get_PerOrgOnlyRep_DerivesRoleFromMembership()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var role = await ctx.CreateGrantingRoleAsync("Per-Org Sales Role");
        // A rep granted ONLY via an organization membership (created outside the module): contact + account
        // without any global role + a membership carrying the granting role.
        await ctx.SeedContactAsync("per-org-contact", c =>
        {
            c.FirstName = "Peron";
            c.LastName = "Rep";
            c.Name = "Peron Rep";
            c.FullName = "Peron Rep";
        });
        var userId = await ctx.CreateAccountWithoutRolesAsync("per-org-contact", "per-org@test.com");
        await ctx.AddMembershipAsync(userId, "org-1", role);

        var details = SalesRepTestContext.Unwrap(await ctx.Controller.Get("per-org-contact"));

        details.Should().NotBeNull();
        details.HasGlobalSalesRepRole.Should().BeFalse("the rep holds no global granting role");
        details.RoleId.Should().Be(role.Id, "with no global role, the role is derived from the granting membership");
        details.RoleName.Should().Be(role.Name);
        details.Organizations.Select(o => o.OrganizationId).Should().BeEquivalentTo(["org-1"]);
    }

    [Fact]
    public async Task Search_IncludesPerOrgOnlyReps()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var role = await ctx.CreateGrantingRoleAsync("Per-Org Sales Role");
        var globalRep = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("global@test.com", "org-1")));
        await ctx.SeedContactAsync("per-org-contact", c => c.Name = "Peron Rep");
        var userId = await ctx.CreateAccountWithoutRolesAsync("per-org-contact", "per-org@test.com");
        await ctx.AddMembershipAsync(userId, "org-1", role);

        // The search is the union of global-role reps (source A) and per-org-membership reps (source B);
        // a rep granted only via a membership must appear alongside the global-role rep.
        var result = SalesRepTestContext.Unwrap(await ctx.Controller.Search(new SalesRepSearchCriteria { Take = 100 }));

        result.Results.Select(r => r.Id).Should().Contain([globalRep.Id, "per-org-contact"]);
        var perOrgItem = result.Results.Single(r => r.Id == "per-org-contact");
        perOrgItem.HasGlobalSalesRepRole.Should().BeFalse();
        perOrgItem.OrganizationsCount.Should().Be(1);
        result.Results.Single(r => r.Id == globalRep.Id).HasGlobalSalesRepRole.Should().BeTrue();
    }

    // ---- membership preservation on org removal ----

    [Fact]
    public async Task Update_RemovingOrg_KeepsMembershipHoldingUnrelatedRoles()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-keep", "org-drop");
        var rep = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("dual@test.com", "org-keep", "org-drop")));

        // The rep also holds an UNRELATED role in org-drop (e.g. they are a buyer there) — attached to the same
        // membership row the sales-rep role lives on.
        var buyerRole = await ctx.CreateNonGrantingRoleAsync("Org Buyer");
        var memberships = await ctx.GetMembershipsAsync(rep.UserId);
        var dropMembership = memberships.Single(m => m.OrganizationId == "org-drop");
        var buyerMembershipRole = AbstractTypeFactory<OrganizationMembershipRole>.TryCreateInstance();
        buyerMembershipRole.RoleId = buyerRole.Id;
        buyerMembershipRole.RoleName = buyerRole.Name;
        dropMembership.Roles = [.. dropMembership.Roles, buyerMembershipRole];
        await ctx.GetRequiredService<IOrganizationMembershipService>().SaveChangesAsync([dropMembership]);

        // Remove org-drop from the rep's served organizations.
        rep.Organizations = [Org("org-keep")];
        var updated = SalesRepTestContext.Unwrap(await ctx.Controller.Update(rep));

        updated.Organizations.Select(o => o.OrganizationId).Should().BeEquivalentTo(["org-keep"]);

        // The org-drop membership must survive with ONLY the unrelated role — revoking the sales-rep assignment
        // must not destroy the user's other roles in that organization.
        var after = await ctx.GetMembershipsAsync(rep.UserId);
        var kept = after.SingleOrDefault(m => m.OrganizationId == "org-drop");
        kept.Should().NotBeNull("a membership holding unrelated roles must not be deleted when the org is unserved");
        kept.Roles.Select(r => r.RoleId).Should().BeEquivalentTo([buyerRole.Id]);
        after.Single(m => m.OrganizationId == "org-keep").Roles.Select(r => r.RoleId).Should().Contain(rep.RoleId);
    }

    // ---- organization-scoped search ----

    [Fact]
    public async Task Search_WithOrganizationId_ScopesResultsButCountsAllServedOrgs()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var repBoth = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("both@test.com", "org-1", "org-2")));
        var repOther = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("other@test.com", "org-2")));

        // Scoped to org-1: only the rep serving it — and their OrganizationsCount reflects ALL served orgs (2),
        // not just the scoped one.
        var org1 = SalesRepTestContext.Unwrap(await ctx.Controller.Search(new SalesRepSearchCriteria { OrganizationId = "org-1", Take = 100 }));
        org1.TotalCount.Should().Be(1);
        org1.Results.Select(r => r.Id).Should().BeEquivalentTo([repBoth.Id]);
        org1.Results.Single().OrganizationsCount.Should().Be(2, "the count shows all organizations the rep serves, not only the scoped one");

        // Scoped to org-2: both reps.
        var org2 = SalesRepTestContext.Unwrap(await ctx.Controller.Search(new SalesRepSearchCriteria { OrganizationId = "org-2", Take = 100 }));
        org2.Results.Select(r => r.Id).Should().BeEquivalentTo([repBoth.Id, repOther.Id]);
    }

    [Fact]
    public async Task Search_WithTakeZero_ReturnsCountOnly()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("one@test.com", "org-1")));
        SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("two@test.com", "org-1")));

        // Take=0 follows the platform search convention: count only — TotalCount populated, no results.
        // Both paging paths must honor it: the member-backed sort (default) …
        var memberSorted = SalesRepTestContext.Unwrap(await ctx.Controller.Search(new SalesRepSearchCriteria { Take = 0 }));
        memberSorted.TotalCount.Should().Be(2);
        memberSorted.Results.Should().BeEmpty("Take=0 is a count-only request");

        // … and the account-backed (row) sort.
        var rowSorted = SalesRepTestContext.Unwrap(await ctx.Controller.Search(new SalesRepSearchCriteria { Take = 0, Sort = "email:asc" }));
        rowSorted.TotalCount.Should().Be(2);
        rowSorted.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_WithNonExistentOrganizationId_ReturnsEmpty()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("some@test.com", "org-1")));

        var result = SalesRepTestContext.Unwrap(await ctx.Controller.Search(new SalesRepSearchCriteria { OrganizationId = "no-such-org", Take = 100 }));

        result.TotalCount.Should().Be(0);
        result.Results.Should().BeEmpty();
    }

    // ---- account endpoints: block / unblock / password ----

    [Fact]
    public async Task BlockAndUnblock_ToggleAccountLockout()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("toggle@test.com")));
        rep.IsLocked.Should().BeFalse();

        await ctx.Controller.Block(rep.Id);
        SalesRepTestContext.Unwrap(await ctx.Controller.Get(rep.Id)).IsLocked.Should().BeTrue();

        await ctx.Controller.Unblock(rep.Id);
        SalesRepTestContext.Unwrap(await ctx.Controller.Get(rep.Id)).IsLocked.Should().BeFalse();

        await using var sdb = ctx.NewSecurityDbContext();
        var user = await sdb.Set<ApplicationUser>().SingleAsync(x => x.MemberId == rep.Id);
        (user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow).Should().BeTrue("unblock must clear the lockout");
    }

    [Fact]
    public async Task SetPassword_ReplacesPassword()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("pwd@test.com"))); // created with P@ssw0rd123!

        await ctx.Controller.SetPassword(rep.Id, new SetPasswordRequest { Password = "N3w-P@ssw0rd!" });

        using var userManager = ctx.GetRequiredService<Func<UserManager<ApplicationUser>>>()();
        var user = await userManager.FindByIdAsync(rep.UserId);
        (await userManager.CheckPasswordAsync(user, "P@ssw0rd123!")).Should().BeFalse("the old password must stop working");
        (await userManager.CheckPasswordAsync(user, "N3w-P@ssw0rd!")).Should().BeTrue("the new password must work");
    }

    [Fact]
    public async Task AccountActions_WithoutAccount_ThrowNoAccountFound()
    {
        using var ctx = SalesRepTestContext.Create();
        // A bare contact with no login account — block must fail with the module's clear message, not an NRE.
        await ctx.SeedContactAsync("bare-contact");

        var act = () => ctx.Controller.Block("bare-contact");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No account found*");
    }

    // ---- update edge cases ----

    [Fact]
    public async Task Update_WithIsLockedFalse_UnblocksBlockedAccount()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("relock@test.com")));
        await ctx.Controller.Block(rep.Id);

        // Saving the rep with IsLocked=false (the blade's unchecked "Blocked" box) must clear the lockout.
        var edit = SalesRepTestContext.Unwrap(await ctx.Controller.Get(rep.Id));
        edit.IsLocked.Should().BeTrue("precondition: the account is blocked");
        edit.IsLocked = false;
        var updated = SalesRepTestContext.Unwrap(await ctx.Controller.Update(edit));

        updated.IsLocked.Should().BeFalse("an update with IsLocked=false must unblock the account");
    }

    [Fact]
    public async Task Update_NonExistentId_ThrowsNotFoundMessage()
    {
        using var ctx = SalesRepTestContext.Create();

        var act = () => ctx.Controller.Update(new SalesRepDetails { Id = "no-such-rep", Emails = ["x@test.com"] });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Create_WithoutLoginEmail_ThrowsClearMessage_AndPersistsNothing()
    {
        using var ctx = SalesRepTestContext.Create();

        // No emails and no user name -> no login identifier. Must fail fast with the module's message (before
        // the contact is saved), not with an opaque Identity error after.
        var act = () => ctx.Controller.Create(new SalesRepDetails { FirstName = "No", LastName = "Login", Emails = [] });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*login email*");

        await using var cdb = ctx.NewCustomerDbContext();
        (await cdb.Set<ContactEntity>().CountAsync()).Should().Be(0, "the guard must reject the create before anything is persisted");
    }

    // ---- read endpoints ----

    [Fact]
    public async Task Get_NonExistentId_ReturnsNotFound()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("direct-get@test.com")));

        // Direct GET of an existing rep returns the aggregate…
        var ok = await ctx.Controller.Get(rep.Id);
        SalesRepTestContext.Unwrap(ok).Id.Should().Be(rep.Id);

        // …and a non-existent id yields 404, not a null-bodied 200.
        var notFound = await ctx.Controller.Get("no-such-rep");
        notFound.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetRoles_IsReadOnly_DoesNotSeedRole()
    {
        using var ctx = SalesRepTestContext.Create();

        // With zero granting roles in the system, GET /roles must return empty WITHOUT lazily seeding the default
        // role — GET endpoints are side-effect free (the default role is seeded at module startup instead).
        var roles = SalesRepTestContext.Unwrap(await ctx.Controller.GetRoles());
        roles.Should().BeEmpty();

        using var roleManager = ctx.GetRequiredService<Func<RoleManager<Role>>>()();
        roleManager.Roles.Count().Should().Be(0, "reading the selectable roles must not create any role");
    }
}
