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

    /// <summary>
    /// Whose tasks the caller may see and change. A rep manages only their own; override to widen the set (a team lead
    /// seeing their reps' tasks) without touching any call site. Empty means "nothing" - never "everything".
    /// </summary>
    protected virtual Task<IList<string>> GetVisibleResponsibleIdsAsync(string userId, string memberId)
    {
        IList<string> result = string.IsNullOrEmpty(memberId) ? [] : [memberId];

        return Task.FromResult(result);
    }
}
