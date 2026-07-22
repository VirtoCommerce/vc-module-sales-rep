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

        var roleIds = roleManager.Roles.Select(x => x.Id).ToList();

        List<Role> granting = [];
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

    public virtual Task<IList<Role>> GetSelectableRolesAsync()
    {
        return GetRolesGrantingAccessAsync();
    }

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

        _grantingRolesCache = null;
        return role;
    }
}
