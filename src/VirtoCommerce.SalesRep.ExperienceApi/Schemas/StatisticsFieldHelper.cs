using System.Threading.Tasks;
using GraphQL;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

/// <summary>
/// Shared money + percentage helpers for the Sales Rep statistics graph types (order, cart and customer widgets),
/// so the MoneyType resolution and the period-over-period math live in one place instead of being copied per type.
/// </summary>
internal static class StatisticsFieldHelper
{
    /// <summary>
    /// Reads the single, optional <see cref="SalesRepFilters.ArgumentName"/> rule name from a statistics field — the
    /// one place the graph types read the filter argument. Null/empty when omitted (the baseline set). The value
    /// doubles as the filter component of the per-block DataLoader key (resolution happens once per distinct name).
    /// </summary>
    public static string GetFilter(IResolveFieldContext context)
        => context.GetArgument<string>(SalesRepFilters.ArgumentName);

    /// <summary>
    /// Resolves a raw decimal (already converted to <paramref name="currencyCode"/> by the service) into a domain
    /// <see cref="Money"/> for the Xapi <c>MoneyType</c> (amount + formattedAmount + currency), formatted for the
    /// given culture. <c>GetAllCurrenciesAsync</c> is cached, so this is safe per field without a DataLoader.
    /// </summary>
    public static async Task<object> ToMoneyAsync(ICurrencyService currencyService, string currencyCode, string cultureName, decimal amount)
    {
        var currencies = await currencyService.GetAllCurrenciesAsync();
        var currency = currencies.GetCurrencyForLanguage(currencyCode, cultureName);
        return new Money(amount, currency);
    }

    /// <summary>Percentage change from a baseline; null when the baseline is zero (no meaningful ratio).</summary>
    public static decimal? Percent(decimal previous, decimal current)
        => previous == 0m ? null : (current - previous) / previous * 100m;
}
