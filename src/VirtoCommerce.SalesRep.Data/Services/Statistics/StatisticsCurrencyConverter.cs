using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

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

            total += new Money(group.Total, sourceCurrency).ConvertTo(targetCurrency).InternalAmount;
            count += group.Count;

            earliestDate = EarlierOf(earliestDate, group.EarliestDate);
            latestDate = LaterOf(latestDate, group.LatestDate);
        }

        var roundedTotal = Math.Round(total, targetCurrency.DecimalDigits, MidpointRounding.AwayFromZero);
        var average = count == 0
            ? 0m
            : Math.Round(total / count, targetCurrency.DecimalDigits, MidpointRounding.AwayFromZero);

        return new FoldedStatistics(roundedTotal, count, average, earliestDate, latestDate, targetCurrency.Code);
    }

    private static DateTime? EarlierOf(DateTime? current, DateTime? candidate)
        => candidate != null && (current == null || candidate < current) ? candidate : current;

    private static DateTime? LaterOf(DateTime? current, DateTime? candidate)
        => candidate != null && (current == null || candidate > current) ? candidate : current;
}
