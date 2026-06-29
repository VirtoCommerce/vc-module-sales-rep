using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepService
{
    /// <summary>Load a Sales Rep aggregate by its contact member id.</summary>
    Task<SalesRepDetails> GetByIdAsync(string id);

    /// <summary>
    /// Create (when <see cref="SalesRepDetails.Id"/> is empty) or update a Sales Rep:
    /// contact profile + login account + global role + per-organization memberships.
    /// </summary>
    Task<SalesRepDetails> SaveChangesAsync(SalesRepDetails salesRep);

    /// <summary>Delete Sales Reps by contact member ids (cascades to the security account).</summary>
    Task DeleteAsync(string[] ids);

    /// <summary>Block (lock out) the rep's account.</summary>
    Task BlockAsync(string id);

    /// <summary>Unblock the rep's account.</summary>
    Task UnblockAsync(string id);

    /// <summary>Set a new password for the rep's account.</summary>
    Task SetPasswordAsync(string id, string newPassword);

    /// <summary>
    /// Roles selectable for a Sales Rep (those granting "sales-rep:access"). Seeds a default role
    /// when none currently grants the permission, so the list is never empty.
    /// </summary>
    Task<IList<SalesRepRole>> GetRolesAsync();
}
