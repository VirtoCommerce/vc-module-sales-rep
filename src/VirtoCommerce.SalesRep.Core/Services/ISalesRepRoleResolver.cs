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
    /// Roles selectable in the UI for a Sales Rep (those granting the permission). Read-only: does NOT seed.
    /// The default role is seeded once at module startup (see <see cref="EnsureSalesRepRoleAsync"/>).
    /// </summary>
    Task<IList<Role>> GetSelectableRolesAsync();

    /// <summary>
    /// Returns a role granting the permission, creating a default one (with a random id) only when none
    /// currently does. Never creates a second granting role.
    /// </summary>
    Task<Role> EnsureSalesRepRoleAsync();
}
