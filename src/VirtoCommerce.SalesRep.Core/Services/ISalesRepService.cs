using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// CRUD for the Sales Rep aggregate (a Contact plus its security account, roles and organization memberships):
/// load, create, update and delete a rep by its contact member id, via the platform
/// <see cref="ICrudService{T}"/> contract (batch <c>GetAsync</c> / <c>SaveChangesAsync</c> / <c>DeleteAsync</c>;
/// the single-item <c>GetByIdAsync</c> convenience is a <c>CrudServiceExtensions</c> extension).
/// </summary>
/// <remarks>
/// The aggregate is not a single persisted entity (it spans the customer, security and membership stores), so the
/// <c>responseGroup</c> / <c>clone</c> (get) and <c>softDelete</c> (delete) parameters are accepted for contract
/// compatibility but not honored: reads always load the full aggregate and deletes always hard-cascade.
/// </remarks>
public interface ISalesRepService : ICrudService<SalesRepDetails>
{
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
