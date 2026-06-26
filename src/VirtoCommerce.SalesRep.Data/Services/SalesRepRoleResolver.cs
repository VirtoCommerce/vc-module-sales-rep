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

    public virtual async Task<Role> EnsureSalesRepRoleAsync()
    {
        var granting = await GetRolesGrantingAccessAsync();

        // Prefer the default seeded role (by stable id) when it still grants access; else any granting role.
        var assignable = granting.FirstOrDefault(x => x.Id == ModuleConstants.Security.Roles.SalesRepRoleId)
                         ?? granting.FirstOrDefault();
        if (assignable != null)
        {
            return assignable;
        }

        // Nothing grants the permission yet — seed a sensible default (create-if-absent, never reseeded).
        var role = AbstractTypeFactory<Role>.TryCreateInstance();
        role.Id = ModuleConstants.Security.Roles.SalesRepRoleId;
        role.Name = ModuleConstants.Security.Roles.SalesRepRoleName;
        role.Permissions = [new Permission { Name = AccessPermission }];

        using var roleManager = _roleManagerFactory();
        var existing = await roleManager.FindByIdAsync(role.Id);
        if (existing == null)
        {
            var result = await roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
            return role;
        }

        // A role with the default id exists but lacks the permission — add it.
        existing.Permissions = [.. existing.Permissions ?? [], new Permission { Name = AccessPermission }];
        await roleManager.UpdateAsync(existing);
        return existing;
    }
}
