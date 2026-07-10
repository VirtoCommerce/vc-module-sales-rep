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

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerStatisticsQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomerStatisticsQuery, CustomerOrderStatisticsContext>
{
    private readonly IStoreService _storeService;
    private readonly ICurrencyService _currencyService;

    public SalesRepCustomerStatisticsQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        IStoreService storeService,
        ICurrencyService currencyService)
        : base(roleResolver, membershipSearchService)
    {
        _storeService = storeService;
        _currencyService = currencyService;
    }

    public virtual async Task<CustomerOrderStatisticsContext> Handle(SalesRepCustomerStatisticsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.OrganizationId))
        {
            return null;
        }

        // Security scoping: the caller must hold an active sales-rep-granting membership in exactly the requested
        // organization. Without this a rep could read any organization's sales figures by guessing its id.
        // OnlyUnlocked: a rep locked in an organization must not see it as a customer.
        var memberships = await GetGrantingMembershipsAsync([request.UserId], [request.OrganizationId]);
        if (memberships.Count == 0)
        {
            return null;
        }

        var currencyCode = request.CurrencyCode;
        if (string.IsNullOrEmpty(currencyCode))
        {
            currencyCode = await ResolveDefaultCurrencyCodeAsync(request.StoreId);
        }

        var result = AbstractTypeFactory<CustomerOrderStatisticsContext>.TryCreateInstance();
        result.OrganizationId = request.OrganizationId;
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
