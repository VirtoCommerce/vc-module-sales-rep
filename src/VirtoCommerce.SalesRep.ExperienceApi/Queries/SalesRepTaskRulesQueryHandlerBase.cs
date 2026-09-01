using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Rule vocabulary for tasks. Unlike the order and customer rules this does NOT narrow by organization: a task belongs
/// to a person, not to an organization, so the rules a rep is offered do not depend on who they serve.
/// </summary>
public abstract class SalesRepTaskRulesQueryHandlerBase<TQuery, TRule> : SalesRepTaskHandlerBase, IQueryHandler<TQuery, IList<TRule>>
    where TQuery : Query<IList<TRule>>, ISalesRepRulesQuery
{
    protected SalesRepTaskRulesQueryHandlerBase(ISalesRepOrganizationAccessService organizationAccessService)
        : base(organizationAccessService)
    {
    }

    public virtual async Task<IList<TRule>> Handle(TQuery request, CancellationToken cancellationToken)
    {
        if (!await IsSalesRepAsync(request.UserId))
        {
            return [];
        }

        return await GetRulesAsync(request);
    }

    protected abstract Task<IList<TRule>> GetRulesAsync(TQuery request);
}
