using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

/// <summary>
/// Shared handler for the standalone sales-rep statistics queries (orders, carts). Resolves the organizations the
/// caller may aggregate — the one requested customer (only if the rep serves it), or every organization the rep is
/// assigned to when none is given — and builds the security-scoped <typeparamref name="TContext"/> backing object
/// (creator scope, store, and resolved target currency). Returns null when the caller has no identity or serves
/// none of the requested organizations, so the field resolves to null.
/// </summary>
public abstract class SalesRepStatisticsQueryHandlerBase<TQuery, TContext> : SalesRepQueryHandlerBase, IQueryHandler<TQuery, TContext>
    where TQuery : Query<TContext>, ISalesRepStatisticsQuery
    where TContext : SalesRepMonetaryStatisticsContext
{
    private readonly ISalesRepCurrencyResolver _currencyResolver;

    protected SalesRepStatisticsQueryHandlerBase(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepCurrencyResolver currencyResolver)
        : base(roleResolver, membershipSearchService)
    {
        _currencyResolver = currencyResolver;
    }

    public virtual async Task<TContext> Handle(TQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId))
        {
            return null;
        }

        // Which organizations to aggregate: the one requested customer (only if the rep serves it), or — when no
        // customer is specified — every organization the rep is assigned to (the combined cross-customer view).
        // Empty means the rep serves none (or doesn't serve the requested one) → no statistics.
        var organizationIds = await GetVisibleOrganizationIdsAsync(request.UserId, request.OrganizationId);
        if (organizationIds.Count == 0)
        {
            return null;
        }

        var result = AbstractTypeFactory<TContext>.TryCreateInstance();
        result.OrganizationIds = organizationIds;
        // Creator scoping: the rep sees statistics only for records they created (data-isolation invariant).
        result.SalesRepUserId = request.UserId;
        result.StoreId = request.StoreId;
        // Client currency wins; else the store's default; else the platform primary. The statistics service throws
        // if the resolved currency has no configured rate.
        result.CurrencyCode = await _currencyResolver.ResolveCurrencyCodeAsync(request.CurrencyCode, request.StoreId);
        return result;
    }
}
