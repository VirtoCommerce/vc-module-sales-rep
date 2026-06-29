using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.SalesRep.Core.Services;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepRoleResolver : ISalesRepRoleResolver
{
    private const string AccessPermission = ModuleConstants.Security.Permissions.Access;

    private readonly Func<RoleManager<Role>> _roleManagerFactory;

    // Memoized for the lifetime of this (transient, per-request) resolver. A single SaveChanges/GetById
    // queries the granting-role set several times; the scan below is the expensive part. Invalidated when
    // EnsureSalesRepRoleAsync creates a new granting role.
    private IList<Role> _grantingRolesCache;

    public SalesRepRoleResolver(Func<RoleManager<Role>> roleManagerFactory)
    {
        _roleManagerFactory = roleManagerFactory;
    }

    public virtual async Task<IList<Role>> GetRolesGrantingAccessAsync()
    {
        return _grantingRolesCache ??= await LoadRolesGrantingAccessAsync();
    }

    protected virtual async Task<IList<Role>> LoadRolesGrantingAccessAsync()
    {
        using var roleManager = _roleManagerFactory();

        // RoleManager.Roles returns role stubs without permissions; FindByIdAsync loads (cached) permission claims.
        var roleIds = roleManager.Roles.Select(x => x.Id).ToList();

        var granting = new List<Role>();
        foreach (var roleId in roleIds)
        {
            var role = await roleManager.FindByIdAsync(roleId);
            if (role?.Permissions?.Any(p => p.Name == AccessPermission) == true)
            {
                granting.Add(role);
            }
        }

        return granting;
    }

    public virtual async Task<ISet<string>> GetRoleIdsGrantingAccessAsync()
    {
        var roles = await GetRolesGrantingAccessAsync();
        return roles.Select(x => x.Id).ToHashSet();
    }

    public virtual async Task<IList<Role>> GetSelectableRolesAsync()
    {
        var roles = await GetRolesGrantingAccessAsync();

        // Lazy seed: if no role grants the permission yet, create the default so the picker is never empty.
        if (roles.Count == 0)
        {
            roles = [await EnsureSalesRepRoleAsync()];
        }

        return roles;
    }

    /// <summary>
    /// Returns a role granting the permission, creating a default one ONLY when none currently does.
    /// The created role gets a random (GUID) id — never a well-known constant — so nothing keys off the id;
    /// a Sales Rep is identified by holding the permission. Because a granting role then exists, subsequent
    /// calls return it instead of creating another, so an admin can delete the built-in role and replace it
    /// with their own without it being re-seeded.
    /// </summary>
    public virtual async Task<Role> EnsureSalesRepRoleAsync()
    {
        var granting = await GetRolesGrantingAccessAsync();
        if (granting.Count > 0)
        {
            return granting[0];
        }

        using var roleManager = _roleManagerFactory();

        var role = AbstractTypeFactory<Role>.TryCreateInstance();
        role.Id = Guid.NewGuid().ToString("N");
        role.Name = ModuleConstants.Security.Roles.SalesRepRoleName;
        role.Description = "Grants Sales Rep access (sales-rep:access).";
        role.Permissions = [new Permission { Name = AccessPermission }];

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        _grantingRolesCache = null; // a new granting role now exists — drop the memoized (empty) set
        return role;
    }
}
