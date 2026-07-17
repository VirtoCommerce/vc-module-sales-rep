using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

public class SalesRepCustomerOrderStatisticsQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomerOrderStatisticsQuery, CustomerOrderStatisticsContext>
{
    private readonly ISalesRepCurrencyResolver _currencyResolver;

    public SalesRepCustomerOrderStatisticsQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepCurrencyResolver currencyResolver)
        : base(roleResolver, membershipSearchService)
    {
        _currencyResolver = currencyResolver;
    }

    public virtual async Task<CustomerOrderStatisticsContext> Handle(SalesRepCustomerOrderStatisticsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId))
        {
            return null;
        }

        // Which organizations to aggregate: the one requested customer (only if the rep serves it), or — when no
        // customer is specified — every organization the rep is assigned to (the combined cross-customer view).
        // Empty means the rep serves none (or doesn't serve the requested one) → no statistics.
        var organizationIds = await GetVisibleOrganizationIdsAsync(request.UserId, request.OrganizationId);
        if (organizationIds.Length == 0)
        {
            return null;
        }

        var result = AbstractTypeFactory<CustomerOrderStatisticsContext>.TryCreateInstance();
        result.OrganizationIds = organizationIds;
        // Creator scoping: the rep sees statistics only for orders they created (data-isolation invariant).
        result.SalesRepUserId = request.UserId;
        result.StoreId = request.StoreId;
        // Client currency wins; else the store's default; else the platform primary. The statistics service throws
        // if the resolved currency has no configured rate.
        result.CurrencyCode = await _currencyResolver.ResolveCurrencyCodeAsync(request.CurrencyCode, request.StoreId);
        return result;
    }
}
