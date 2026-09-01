using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Rule vocabulary for tasks. Reuses the shared pipeline and redefines only the scope: unlike the order and customer
/// rules this does NOT narrow by organization, because a task belongs to a person, not to an organization — the rules
/// a rep is offered do not depend on who they serve, only on their being a rep.
/// </summary>
public abstract class SalesRepTaskRulesQueryHandlerBase<TQuery, TRule> : SalesRepRulesQueryHandlerBase<TQuery, TRule>
    where TQuery : Query<IList<TRule>>, ISalesRepRulesQuery
{
    protected SalesRepTaskRulesQueryHandlerBase(ISalesRepOrganizationAccessService organizationAccessService)
        : base(organizationAccessService)
    {
    }

    /// <summary>Empty scope means "allowed, but the vocabulary is not data-derived"; null would deny.</summary>
    protected override async Task<IList<string>> ResolveScopeAsync(TQuery request)
    {
        return await IsSalesRepAsync(request.UserId) ? [] : null;
    }
}
