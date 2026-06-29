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

    public SalesRepRoleResolver(Func<RoleManager<Role>> roleManagerFactory)
    {
        _roleManagerFactory = roleManagerFactory;
    }

    public virtual async Task<IList<Role>> GetRolesGrantingAccessAsync()
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

    public virtual async Task<Role> GetAssignableRoleAsync(string roleId)
    {
        if (!string.IsNullOrEmpty(roleId))
        {
            using var roleManager = _roleManagerFactory();
            var role = await roleManager.FindByIdAsync(roleId);
            if (role?.Permissions?.Any(p => p.Name == AccessPermission) == true)
            {
                return role;
            }
        }

        // No (valid) role selected — fall back to the lazily seeded default.
        return await EnsureSalesRepRoleAsync();
    }

    /// <summary>
    /// The single role used for assignment (global and per-organization). Deterministic by stable id
    /// (<c>sales-rep</c>) so there is never ambiguity when several roles grant the permission.
    /// Seeded once (create-if-absent, never reseeded → admins may rename it); if the seeded role exists
    /// but lost the permission, it is re-added so assigned reps keep their access.
    /// </summary>
    public virtual async Task<Role> EnsureSalesRepRoleAsync()
    {
        using var roleManager = _roleManagerFactory();

        var existing = await roleManager.FindByIdAsync(ModuleConstants.Security.Roles.SalesRepRoleId);
        if (existing != null)
        {
            if (existing.Permissions?.Any(p => p.Name == AccessPermission) != true)
            {
                existing.Permissions = [.. existing.Permissions ?? [], new Permission { Name = AccessPermission }];
                await roleManager.UpdateAsync(existing);
            }
            return existing;
        }

        var role = AbstractTypeFactory<Role>.TryCreateInstance();
        role.Id = ModuleConstants.Security.Roles.SalesRepRoleId;
        role.Name = ModuleConstants.Security.Roles.SalesRepRoleName;
        role.Description = "Grants Sales Rep access (sales-rep:access).";
        role.Permissions = [new Permission { Name = AccessPermission }];

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        return role;
    }
}
