using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>Every store offers the harness's fixed currency set (USD primary, EUR).</summary>
internal sealed class CurrencyServiceStoreCurrencyResolver(ICurrencyService currencyService) : IStoreCurrencyResolver
{
    public async Task<Currency> GetStoreCurrencyAsync(string currencyCode, string storeId, string cultureName = null)
        => (await currencyService.GetAllCurrenciesAsync()).FirstOrDefault(x => x.Code.EqualsIgnoreCase(currencyCode));

    public async Task<IEnumerable<Currency>> GetAllStoreCurrenciesAsync(string storeId, string cultureName = null)
        => await currencyService.GetAllCurrenciesAsync();
}
