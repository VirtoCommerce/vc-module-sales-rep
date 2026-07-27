using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Base for the rule-discovery handlers (order/cart/customer/top-seller filter and sort rules). The rule vocabulary
/// is sales-rep-only, so — like the statistics handlers — it requires the caller to hold at least one granting
/// sales-rep membership. A merely-authenticated user (e.g. a regular buyer) gets an empty list instead of being able
/// to enumerate the vocabulary (which for the top-seller filter rules would expose the store's top-level catalog
/// categories). Concrete handlers supply the rules via <see cref="GetRulesAsync"/>.
/// </summary>
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

        var organizationIds = await GetServedOrganizationIdsAsync(request.UserId);
        if (organizationIds.Count == 0)
        {
            return [];
        }

        return await GetRulesAsync(request);
    }

    protected abstract Task<IList<TRule>> GetRulesAsync(TQuery request);
}
