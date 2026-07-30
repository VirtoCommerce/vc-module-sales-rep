using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public abstract class SalesRepRulesQueryHandlerBase<TQuery, TRule> : SalesRepQueryHandlerBase, IQueryHandler<TQuery, IList<TRule>>
    where TQuery : Query<IList<TRule>>, ISalesRepRulesQuery
{
    protected SalesRepRulesQueryHandlerBase(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService)
        : base(roleResolver, membershipSearchService)
    {
    }

    public virtual async Task<IList<TRule>> Handle(TQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId))
        {
            return [];
        }

        // Rule vocabulary is sales-rep-only: a caller with no granting membership gets an empty list, never the
        // vocabulary — otherwise e.g. the top-seller filter rules would leak the store's top-level catalog categories.
        var organizationIds = await GetServedOrganizationIdsAsync(request.UserId);
        if (organizationIds.Count == 0)
        {
            return [];
        }

        return await GetRulesAsync(request, organizationIds);
    }

    /// <param name="organizationIds">The organizations the caller serves — the scope a data-derived rule set must be
    /// built within, so it only offers rules the caller's own lists can return records for.</param>
    protected abstract Task<IList<TRule>> GetRulesAsync(TQuery request, IList<string> organizationIds);
}
