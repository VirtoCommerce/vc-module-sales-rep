using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

/// <summary>Raw per-currency aggregate (sum + count + earliest/latest date) read from a module's EF store before conversion.</summary>
internal sealed class CurrencyStatisticAggregate
{
    public string Currency { get; set; }
    public decimal Total { get; set; }
    public int Count { get; set; }
    public DateTime? EarliestDate { get; set; }
    public DateTime? LatestDate { get; set; }
}

/// <summary>Sum / count / average folded into one currency, plus that currency's code and the earliest/latest record date.</summary>
internal sealed record FoldedStatistics(decimal Total, int Count, decimal Average, DateTime? EarliestDate, DateTime? LatestDate, string CurrencyCode);

/// <summary>
/// Shared currency fold for the Sales Rep statistics services (orders, carts). Converts a set of per-currency
/// aggregates into one target currency via the domain <see cref="Money"/> type (the single source of truth for FX
/// rate math, using the current admin-maintained <c>ExchangeRate</c> values), then rounds to the target's decimal
/// digits. Keeping per-currency counts until the fold is what makes the average correct across a mix of currencies.
/// A source currency with no configured rate is skipped (and logged) rather than blanking the whole widget; the
/// target currency must be configured (the caller resolves the code). The earliest/latest dates are tracked over the
/// same skipped set, so a record in an unconfigured currency contributes to neither them nor the sum/count/average.
/// </summary>
internal static class StatisticsCurrencyConverter
{
    public static FoldedStatistics Fold(
        IEnumerable<CurrencyStatisticAggregate> byCurrency,
        string targetCurrencyCode,
        IReadOnlyCollection<Currency> currencies,
        ILogger logger)
    {
        var targetCurrency = currencies.FirstOrDefault(x => x.Code.EqualsIgnoreCase(targetCurrencyCode))
            ?? throw new InvalidOperationException($"Currency '{targetCurrencyCode}' is not configured; cannot convert sales-rep statistics.");

        var total = 0m;
        var count = 0;
        DateTime? earliestDate = null;
        DateTime? latestDate = null;

        foreach (var group in byCurrency)
        {
            var sourceCurrency = currencies.FirstOrDefault(x => x.Code.EqualsIgnoreCase(group.Currency));
            if (sourceCurrency == null)
            {
                logger.LogWarning("Skipping {Count} record(s) in unconfigured currency '{Currency}' while computing sales-rep statistics.", group.Count, group.Currency);
                continue;
            }

            // InternalAmount keeps the unrounded decimal; the fold is rounded once at the end.
            total += new Money(group.Total, sourceCurrency).ConvertTo(targetCurrency).InternalAmount;
            count += group.Count;

            if (group.EarliestDate != null && (earliestDate == null || group.EarliestDate < earliestDate))
            {
                earliestDate = group.EarliestDate;
            }

            if (group.LatestDate != null && (latestDate == null || group.LatestDate > latestDate))
            {
                latestDate = group.LatestDate;
            }
        }

        var roundedTotal = Math.Round(total, targetCurrency.DecimalDigits, MidpointRounding.AwayFromZero);
        var average = count == 0
            ? 0m
            : Math.Round(total / count, targetCurrency.DecimalDigits, MidpointRounding.AwayFromZero);

        return new FoldedStatistics(roundedTotal, count, average, earliestDate, latestDate, targetCurrency.Code);
    }
}
