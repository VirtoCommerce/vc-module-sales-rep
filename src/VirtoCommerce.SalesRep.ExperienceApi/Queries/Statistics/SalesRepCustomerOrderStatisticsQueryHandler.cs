using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

public class SalesRepCustomerOrderStatisticsQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomerOrderStatisticsQuery, CustomerOrderStatisticsContext>
{
    private readonly IStoreService _storeService;
    private readonly ICurrencyService _currencyService;

    public SalesRepCustomerOrderStatisticsQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        IStoreService storeService,
        ICurrencyService currencyService)
        : base(roleResolver, membershipSearchService)
    {
        _storeService = storeService;
        _currencyService = currencyService;
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

        var currencyCode = request.CurrencyCode;
        if (string.IsNullOrEmpty(currencyCode))
        {
            currencyCode = await ResolveDefaultCurrencyCodeAsync(request.StoreId);
        }

        var result = AbstractTypeFactory<CustomerOrderStatisticsContext>.TryCreateInstance();
        result.OrganizationIds = organizationIds;
        // Creator scoping: the rep sees statistics only for orders they created (data-isolation invariant).
        result.SalesRepUserId = request.UserId;
        result.StoreId = request.StoreId;
        result.CurrencyCode = currencyCode;
        return result;
    }

    // Currency defaulting: the client's currencyCode wins; otherwise the store's default currency, and finally the
    // platform primary currency. The statistics service throws if the resolved currency has no configured rate.
    private async Task<string> ResolveDefaultCurrencyCodeAsync(string storeId)
    {
        if (!string.IsNullOrEmpty(storeId))
        {
            var store = await _storeService.GetByIdAsync(storeId);
            if (!string.IsNullOrEmpty(store?.DefaultCurrency))
            {
                return store.DefaultCurrency;
            }
        }

        var currencies = await _currencyService.GetAllCurrenciesAsync();
        return currencies.FirstOrDefault(x => x.IsPrimary)?.Code;
    }
}
