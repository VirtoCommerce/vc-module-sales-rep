using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

public class SalesRepCustomerCartStatisticsQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomerCartStatisticsQuery, CustomerCartStatisticsContext>
{
    private readonly ISalesRepCurrencyResolver _currencyResolver;

    public SalesRepCustomerCartStatisticsQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepCurrencyResolver currencyResolver)
        : base(roleResolver, membershipSearchService)
    {
        _currencyResolver = currencyResolver;
    }

    public virtual async Task<CustomerCartStatisticsContext> Handle(SalesRepCustomerCartStatisticsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId))
        {
            return null;
        }

        // Membership scoping: the one requested customer (only if the rep serves it), or every organization the rep
        // is assigned to. Empty means the rep serves none (or doesn't serve the requested one) → no statistics.
        var organizationIds = await GetVisibleOrganizationIdsAsync(request.UserId, request.OrganizationId);
        if (organizationIds.Length == 0)
        {
            return null;
        }

        var result = AbstractTypeFactory<CustomerCartStatisticsContext>.TryCreateInstance();
        result.OrganizationIds = organizationIds;
        // Creator scoping: the rep sees statistics only for carts they created (data-isolation invariant).
        result.SalesRepUserId = request.UserId;
        result.StoreId = request.StoreId;
        // Client currency wins; else the store's default; else the platform primary. The statistics service throws
        // if the resolved currency has no configured rate.
        result.CurrencyCode = await _currencyResolver.ResolveCurrencyCodeAsync(request.CurrencyCode, request.StoreId);
        return result;
    }
}
