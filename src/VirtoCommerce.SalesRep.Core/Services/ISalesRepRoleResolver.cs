using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepRoleResolver
{
    Task<IList<Role>> GetRolesGrantingAccessAsync();

    Task<ISet<string>> GetRoleIdsGrantingAccessAsync();

    Task<IList<Role>> GetSelectableRolesAsync();

    Task<Role> EnsureSalesRepRoleAsync();
}
