using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// Resolves which roles currently grant the "sales-rep:access" permission.
/// A Sales Rep is defined by holding that permission (via any role), never by a fixed role id/name,
/// because roles are just permission sets whose id/name may change.
/// </summary>
public interface ISalesRepRoleResolver
{
    /// <summary>Roles whose permission set currently includes "sales-rep:access".</summary>
    Task<IList<Role>> GetRolesGrantingAccessAsync();

    /// <summary>Ids of the roles that currently grant "sales-rep:access".</summary>
    Task<ISet<string>> GetRoleIdsGrantingAccessAsync();

    /// <summary>
    /// Returns a role suitable to assign to a new/edited Sales Rep (one that grants the permission).
    /// Seeds a default "Sales Representative" role when none currently grants it.
    /// </summary>
    Task<Role> EnsureSalesRepRoleAsync();
}
