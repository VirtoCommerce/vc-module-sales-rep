using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public abstract class SalesRepTaskHandlerBase : SalesRepQueryHandlerBase
{
    protected SalesRepTaskHandlerBase(ISalesRepOrganizationAccessService organizationAccessService)
        : base(organizationAccessService)
    {
    }

    // Whose tasks the caller may see and change - the seam for widening beyond their own. Every read and write
    // answers through it. Empty means "nothing", never "everything".
    protected virtual Task<IList<string>> GetVisibleResponsibleIdsAsync(string userId, string memberId)
    {
        IList<string> result = string.IsNullOrEmpty(memberId) ? [] : [memberId];

        return Task.FromResult(result);
    }
}
