using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepDictionaryService : ISalesRepDictionaryService
{
    private readonly ICountriesService _countriesService;
    private readonly ICurrencyService _currencyService;
    private readonly ISettingsManager _settingsManager;

    public SalesRepDictionaryService(
        ICountriesService countriesService,
        ICurrencyService currencyService,
        ISettingsManager settingsManager)
    {
        _countriesService = countriesService;
        _currencyService = currencyService;
        _settingsManager = settingsManager;
    }

    public virtual async Task<SalesRepDictionaries> GetDictionariesAsync()
    {
        var result = AbstractTypeFactory<SalesRepDictionaries>.TryCreateInstance();
        result.Countries = await GetCountriesAsync();
        result.Currencies = await GetCurrenciesAsync();
        result.Languages = await GetLanguagesAsync();
        return result;
    }

    protected virtual async Task<IList<SalesRepCountry>> GetCountriesAsync()
    {
        var countries = await _countriesService.GetCountriesAsync();
        return countries
            .Select(x =>
            {
                var country = AbstractTypeFactory<SalesRepCountry>.TryCreateInstance();
                country.Id = x.Id;
                country.Name = x.Name;
                return country;
            })
            .ToList();
    }

    protected virtual async Task<IList<SalesRepCurrency>> GetCurrenciesAsync()
    {
        var currencies = await _currencyService.GetAllCurrenciesAsync();
        return currencies
            .Select(x =>
            {
                var currency = AbstractTypeFactory<SalesRepCurrency>.TryCreateInstance();
                currency.Code = x.Code;
                currency.Name = x.EnglishName.EmptyToNull() ?? x.Name.EmptyToNull() ?? x.Code;
                currency.Symbol = x.Symbol;
                return currency;
            })
            .ToList();
    }

    /// <summary>The configured storefront languages — the allowed values of the platform "Languages" dictionary
    /// setting (the same source the customer contact admin populates its Language dropdown from).</summary>
    protected virtual async Task<IList<string>> GetLanguagesAsync()
    {
        var setting = await _settingsManager.GetObjectSettingAsync(PlatformConstants.Settings.General.Languages.Name);
        return setting?.AllowedValues?
            .Select(x => x?.ToString())
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList() ?? [];
    }
}
