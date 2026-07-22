using System;
using System.Threading.Tasks;
using GraphQL;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

internal static class StatisticsFieldHelper
{
    public const string FromArgument = "from";

    public const string ToArgument = "to";

    public const string CurrentArgument = "current";

    public const string PreviousArgument = "previous";

    public static string GetFilter(IResolveFieldContext context)
        => context.GetArgument<string>(SalesRepFilters.ArgumentName);

    public static async Task<object> ToMoneyAsync(ICurrencyService currencyService, string currencyCode, string cultureName, decimal amount)
    {
        var currencies = await currencyService.GetAllCurrenciesAsync();
        var currency = currencies.GetCurrencyForLanguage(currencyCode, cultureName);
        return new Money(amount, currency);
    }

    public static decimal? Percent(decimal previous, decimal current)
        => previous == 0m ? null : (current - previous) / previous * 100m;

    public static TPeriod EmptyPeriod<TPeriod>(Action<TPeriod> configure = null) where TPeriod : class
    {
        var period = AbstractTypeFactory<TPeriod>.TryCreateInstance();
        configure?.Invoke(period);
        return period;
    }
}
