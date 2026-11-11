using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// The granting roles are cached across requests with the platform's role entries (VCST-6476), against the real
/// RoleManager on in-memory SQLite: a role change made through the role manager must reach the very next call.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepRoleResolverComponentTests
{
    [Fact]
    public async Task GetRoleIdsGrantingAccessAsync_RoleGainsAccessAfterCaching_IsIncludedOnTheNextCall()
    {
        using var ctx = SalesRepTestContext.Create();
        var resolver = ctx.GetRequiredService<ISalesRepRoleResolver>();
        await resolver.GetRoleIdsGrantingAccessAsync();

        var role = await ctx.CreateGrantingRoleAsync("Field Sales");

        (await resolver.GetRoleIdsGrantingAccessAsync()).Should().Contain(role.Id);
    }

    [Fact]
    public async Task GetRoleIdsGrantingAccessAsync_AccessRevokedAfterCaching_IsExcludedOnTheNextCall()
    {
        using var ctx = SalesRepTestContext.Create();
        var resolver = ctx.GetRequiredService<ISalesRepRoleResolver>();
        var role = await ctx.CreateGrantingRoleAsync("Field Sales");
        (await resolver.GetRoleIdsGrantingAccessAsync()).Should().Contain(role.Id);

        // DATA-ISOLATION INVARIANT: once sales-rep:access is removed, the role must stop granting at once.
        await UpdatePermissionsAsync(ctx, role, "customer:read");

        (await resolver.GetRoleIdsGrantingAccessAsync()).Should().NotContain(role.Id);
    }

    [Fact]
    public async Task GetRoleIdsGrantingAccessAsync_GrantingRoleDeletedAfterCaching_IsExcludedOnTheNextCall()
    {
        using var ctx = SalesRepTestContext.Create();
        var resolver = ctx.GetRequiredService<ISalesRepRoleResolver>();
        var role = await ctx.CreateGrantingRoleAsync("Field Sales");
        (await resolver.GetRoleIdsGrantingAccessAsync()).Should().Contain(role.Id);

        // DATA-ISOLATION INVARIANT: a deleted role must stop granting at once.
        await DeleteRoleAsync(ctx, role);

        (await resolver.GetRoleIdsGrantingAccessAsync()).Should().NotContain(role.Id);
    }

    private static async Task DeleteRoleAsync(SalesRepTestContext ctx, Role role)
    {
        using var roleManager = ctx.GetRequiredService<Func<RoleManager<Role>>>()();

        var result = await roleManager.DeleteAsync(role);
        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors.Select(x => x.Description)));
    }

    private static async Task UpdatePermissionsAsync(SalesRepTestContext ctx, Role role, params string[] permissions)
    {
        using var roleManager = ctx.GetRequiredService<Func<RoleManager<Role>>>()();
        var update = AbstractTypeFactory<Role>.TryCreateInstance();
        update.Id = role.Id;
        update.Name = role.Name;
        update.Permissions = [.. permissions.Select(x => new Permission { Name = x })];

        var result = await roleManager.UpdateAsync(update);
        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
