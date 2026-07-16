using System;
using System.Linq;
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
    // Separator for encoding a resolved filter set into the (value-equatable) DataLoader batch key. Unit Separator
    // (U+001F) never occurs in a status/type/kind value, so join/split round-trips losslessly.
    private const char SetSeparator = '\u001f';

    /// <summary>
    /// Canonical, order-independent encoding of a resolved filter set (statuses, cart types, …) into a DataLoader
    /// batch-key segment ("" = no filter), so two selections that resolve to the same set share one aggregate query.
    /// </summary>
    public static string EncodeSet(string[] values)
        => values == null || values.Length == 0
            ? string.Empty
            : string.Join(SetSeparator, values.OrderBy(x => x, StringComparer.Ordinal));

    /// <summary>Reverses <see cref="EncodeSet"/> ("" → null = no filter).</summary>
    public static string[] DecodeSet(string encoded)
        => string.IsNullOrEmpty(encoded) ? null : encoded.Split(SetSeparator);

    /// <summary>
    /// Reads the unified <see cref="SalesRepFilters.ArgumentName"/> selection from a statistics field and encodes it
    /// into a stable batch-key segment — the one place the graph types read the filter argument.
    /// </summary>
    public static string GetFilterKey(IResolveFieldContext context)
        => EncodeSet(context.GetArgument<string[]>(SalesRepFilters.ArgumentName));

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
