using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// Startup role seeding (VCST-5730 T1) against the real RoleManager on in-memory SQLite: the documents-library
/// roles are seeded by PERMISSION SET (never by name), so pre-existing roles carrying the capability — whatever
/// they are called — suppress seeding, and repeated startups never duplicate. Also pins the two role families
/// apart: the back-office role carries diagnostics but never sales-rep:access.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepRoleSeedingComponentTests
{
    private const string Access = ModuleConstants.Security.Permissions.Access;
    private const string DocumentsRead = ModuleConstants.Security.Permissions.DocumentsRead;
    private const string DocumentsWrite = ModuleConstants.Security.Permissions.DocumentsWrite;
    private const string Diagnostics = ModuleConstants.Security.Permissions.Diagnostics;
    private const string AdvancedRoleName = ModuleConstants.Security.Roles.AdvancedSalesRepRoleName;
    private const string ManagerRoleName = ModuleConstants.Security.Roles.DocumentsManagerRoleName;

    [Fact]
    public async Task Seed_FreshDatabase_CreatesBothRolesWithExactPermissionSets()
    {
        using var ctx = SalesRepTestContext.Create();

        await SeedAsync(ctx);

        var roles = await LoadRolesAsync(ctx);

        var advanced = roles.Single(x => x.Name == AdvancedRoleName);
        advanced.Permissions.Select(x => x.Name).Should().BeEquivalentTo([Access, DocumentsRead]);

        // Read is granted alongside write by role composition — write does not imply read in code. Diagnostics
        // rides on the same back-office role rather than a role of its own; Access deliberately does NOT, since
        // that permission is what makes an OrganizationMembership a rep.
        var manager = roles.Single(x => x.Name == ManagerRoleName);
        manager.Permissions.Select(x => x.Name).Should().BeEquivalentTo([DocumentsRead, DocumentsWrite, Diagnostics]);
        manager.Permissions.Should().NotContain(x => x.Name == Access);
    }

    [Fact]
    public async Task Seed_RoleWithAccessAndDocumentsReadExists_SkipsAdvancedRole()
    {
        using var ctx = SalesRepTestContext.Create();
        // Any name — the rep+documents capability combo is what suppresses seeding, extra permissions included.
        await ctx.CreateRoleAsync("Custom Field Sales", Access, DocumentsRead, "customer:read");

        await SeedAsync(ctx);

        var roles = await LoadRolesAsync(ctx);
        roles.Should().NotContain(x => x.Name == AdvancedRoleName);
        roles.Should().ContainSingle(x => x.Name == ManagerRoleName, "the write capability is still uncovered");
    }

    [Fact]
    public async Task Seed_RoleWithBothDocumentPermissionsExists_SkipsManagerRole()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.CreateRoleAsync("Custom Library Admin", DocumentsRead, DocumentsWrite, Diagnostics);

        await SeedAsync(ctx);

        var roles = await LoadRolesAsync(ctx);
        roles.Should().NotContain(x => x.Name == ManagerRoleName);
        roles.Should().ContainSingle(x => x.Name == AdvancedRoleName, "the rep+read capability is still uncovered");
    }

    // A role carrying the seeded NAME suppresses seeding whatever its permission set: the seeder never fights
    // the administrator — no mutation (an admin may have removed a permission on purpose), no name collision.
    // The name match is case-insensitive, like Identity's NormalizedName uniqueness the guard protects.
    [Fact]
    public async Task Seed_RoleWithSeededNameExists_IsLeftUntouched()
    {
        using var ctx = SalesRepTestContext.Create();
        var caseVariantName = ManagerRoleName.ToUpperInvariant();
        await ctx.CreateRoleAsync(caseVariantName, DocumentsWrite);

        await SeedAsync(ctx);

        var roles = await LoadRolesAsync(ctx);
        var manager = roles.Single(x => x.Name == caseVariantName);
        manager.Permissions.Select(x => x.Name).Should().BeEquivalentTo([DocumentsWrite]);
    }

    // The upgrade path we deliberately did NOT write. An install that seeded the back-office role under an earlier
    // version has it by name with the older permission set, and the name guard leaves it alone — so a permission
    // added to the seeded set reaches fresh installs only, and an upgraded install needs it granted by hand.
    [Fact]
    public async Task Seed_ManagerRoleFromAnEarlierVersionExists_DoesNotGainTheNewPermission()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.CreateRoleAsync(ManagerRoleName, DocumentsRead, DocumentsWrite);

        await SeedAsync(ctx);

        var roles = await LoadRolesAsync(ctx);
        // One role, not two: the rename was skipped precisely so an upgrade cannot end up with both.
        var manager = roles.Should().ContainSingle(x => x.Name == ManagerRoleName).Subject;
        manager.Permissions.Select(x => x.Name).Should().BeEquivalentTo([DocumentsRead, DocumentsWrite]);
    }

    // The permission-set guard is capability-based, so a role covering only part of the set does not suppress it.
    [Fact]
    public async Task Seed_RoleWithDocumentPermissionsButNoDiagnostics_StillSeedsManagerRole()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.CreateRoleAsync("Custom Library Admin", DocumentsRead, DocumentsWrite);

        await SeedAsync(ctx);

        var roles = await LoadRolesAsync(ctx);
        var manager = roles.Should().ContainSingle(x => x.Name == ManagerRoleName).Subject;
        manager.Permissions.Select(x => x.Name).Should().BeEquivalentTo([DocumentsRead, DocumentsWrite, Diagnostics]);
    }

    [Fact]
    public async Task Seed_RunTwice_DoesNotDuplicateRoles()
    {
        using var ctx = SalesRepTestContext.Create();

        await SeedAsync(ctx);
        var afterFirstRun = (await LoadRolesAsync(ctx)).Count;

        await SeedAsync(ctx);

        var roles = await LoadRolesAsync(ctx);
        roles.Should().HaveCount(afterFirstRun);
        roles.Should().ContainSingle(x => x.Name == AdvancedRoleName);
        roles.Should().ContainSingle(x => x.Name == ManagerRoleName);
    }

    /// <summary>Mirrors Module.PostInitialize: base rep role first, then the documents-library roles.</summary>
    private static async Task SeedAsync(SalesRepTestContext ctx)
    {
        await ctx.GetRequiredService<ISalesRepRoleResolver>().EnsureSalesRepRoleAsync();
        await ctx.GetRequiredService<ISalesRepRoleSeeder>().EnsureDocumentRolesAsync();
    }

    private static async Task<IList<Role>> LoadRolesAsync(SalesRepTestContext ctx)
    {
        using var roleManager = ctx.GetRequiredService<Func<RoleManager<Role>>>()();

        var roleIds = roleManager.Roles.Select(x => x.Id).ToList();

        List<Role> roles = [];
        foreach (var roleId in roleIds)
        {
            roles.Add(await roleManager.FindByIdAsync(roleId));
        }

        return roles;
    }
}
