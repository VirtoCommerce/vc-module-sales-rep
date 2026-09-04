using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public abstract class SalesRepRulesQueryHandlerBase<TQuery, TRule> : SalesRepQueryHandlerBase, IQueryHandler<TQuery, IList<TRule>>
    where TQuery : Query<IList<TRule>>, ISalesRepRulesQuery
{
    protected SalesRepRulesQueryHandlerBase(
        ISalesRepOrganizationAccessService organizationAccessService)
        : base(organizationAccessService)
    {
    }

    public virtual async Task<IList<TRule>> Handle(TQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId))
        {
            return [];
        }

        var scope = await ResolveScopeAsync(request);
        if (scope is null)
        {
            return [];
        }

        return await GetRulesAsync(request, scope);
    }

    // The organizations a data-derived vocabulary is built within; null denies. A caller with no granting
    // membership gets an empty list, never the vocabulary — otherwise e.g. the top-seller filter rules would
    // leak the store's top-level catalog categories. A personal-rules query is scoped to the caller instead,
    // where an empty scope means "allowed, but not data-derived".
    protected virtual async Task<IList<string>> ResolveScopeAsync(TQuery request)
    {
        if (request is ISalesRepPersonalRulesQuery)
        {
            return await IsSalesRepAsync(request.UserId) ? [] : null;
        }

        var organizationIds = await OrganizationAccessService.GetVisibleOrganizationIdsAsync(
            request.UserId,
            (request as ISalesRepScopedRulesQuery)?.OrganizationId);

        return organizationIds.Count == 0 ? null : organizationIds;
    }

    /// <param name="organizationIds">The organizations the caller serves — the scope a data-derived rule set must be
    /// built within, so it only offers rules the caller's own lists can return records for.</param>
    protected abstract Task<IList<TRule>> GetRulesAsync(TQuery request, IList<string> organizationIds);
}
