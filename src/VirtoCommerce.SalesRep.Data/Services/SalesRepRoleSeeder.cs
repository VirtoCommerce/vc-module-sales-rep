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

public class SalesRepRoleSeeder : ISalesRepRoleSeeder
{
    private readonly Func<RoleManager<Role>> _roleManagerFactory;

    public SalesRepRoleSeeder(Func<RoleManager<Role>> roleManagerFactory)
    {
        _roleManagerFactory = roleManagerFactory;
    }

    public virtual async Task EnsureDocumentRolesAsync()
    {
        using var roleManager = _roleManagerFactory();

        var roles = await LoadRolesAsync(roleManager);

        await EnsureRoleAsync(
            roleManager,
            roles,
            ModuleConstants.Security.Roles.AdvancedSalesRepRoleName,
            "Grants Sales Rep access and documents library read (sales-rep:access, sales-rep-documents:read).",
            [ModuleConstants.Security.Permissions.Access, ModuleConstants.Security.Permissions.DocumentsRead]);

        await EnsureRoleAsync(
            roleManager,
            roles,
            ModuleConstants.Security.Roles.DocumentsManagerRoleName,
            "Grants Sales Rep documents library management (sales-rep-documents:read, sales-rep-documents:write).",
            [ModuleConstants.Security.Permissions.DocumentsRead, ModuleConstants.Security.Permissions.DocumentsWrite]);
    }

    protected virtual async Task<IList<Role>> LoadRolesAsync(RoleManager<Role> roleManager)
    {
        var roleIds = roleManager.Roles.Select(x => x.Id).ToList();

        List<Role> roles = [];
        foreach (var roleId in roleIds)
        {
            var role = await roleManager.FindByIdAsync(roleId);
            if (role != null)
            {
                roles.Add(role);
            }
        }

        return roles;
    }

    // Matches by permission set, not name/id: any role already carrying every listed permission counts, so renames
    // don't re-seed. A role with the seeded NAME also suppresses seeding whatever its permissions — it is owned by
    // the administrator (or an earlier seeder version) and is never mutated or collided with.
    protected virtual async Task EnsureRoleAsync(RoleManager<Role> roleManager, IList<Role> existingRoles, string name, string description, string[] permissions)
    {
        if (existingRoles.Any(role =>
                role.Name.EqualsIgnoreCase(name) ||
                permissions.All(permission => role.Permissions?.Any(x => x.Name == permission) == true)))
        {
            return;
        }

        var role = AbstractTypeFactory<Role>.TryCreateInstance();
        role.Id = Guid.NewGuid().ToString("N");
        role.Name = name;
        role.Description = description;
        role.Permissions = [.. permissions.Select(x => new Permission { Name = x })];

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
