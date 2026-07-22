using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepCurrencyResolver : ISalesRepCurrencyResolver
{
    private readonly IStoreService _storeService;
    private readonly ICurrencyService _currencyService;

    public SalesRepCurrencyResolver(IStoreService storeService, ICurrencyService currencyService)
    {
        _storeService = storeService;
        _currencyService = currencyService;
    }

    public virtual async Task<string> ResolveCurrencyCodeAsync(string requestedCurrencyCode, string storeId)
    {
        if (!string.IsNullOrEmpty(requestedCurrencyCode))
        {
            return requestedCurrencyCode;
        }

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
