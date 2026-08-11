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

        var excludedCount = 0;
        var excludedCurrencies = new List<string>();

        foreach (var group in byCurrency)
        {
            var sourceCurrency = currencies.FirstOrDefault(x => x.Code.EqualsIgnoreCase(group.Currency));
            if (sourceCurrency == null)
            {
                logger.LogWarning("Skipping {Count} record(s) in unconfigured currency '{Currency}' while computing sales-rep statistics.", group.Count, group.Currency);

                excludedCount += group.Count;
                var label = string.IsNullOrEmpty(group.Currency) ? "unspecified" : group.Currency;
                if (!excludedCurrencies.Contains(label, StringComparer.OrdinalIgnoreCase))
                {
                    excludedCurrencies.Add(label);
                }

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

        return new FoldedStatistics(roundedTotal, count, average, earliestDate, latestDate, targetCurrency.Code, BuildWarning(excludedCount, excludedCurrencies));
    }

    /// <summary>
    /// Rate that brings an amount from one currency into another, zero when either is unconfigured (unconvertible, and
    /// excluded from the figures by <see cref="Fold"/>). Same primitive the fold converts with, so a ranking built on
    /// it cannot disagree with the amounts the fold reports.
    /// </summary>
    public static decimal GetRate(string sourceCurrencyCode, string targetCurrencyCode, IReadOnlyCollection<Currency> currencies)
    {
        var source = currencies.FirstOrDefault(x => x.Code.EqualsIgnoreCase(sourceCurrencyCode));
        var target = currencies.FirstOrDefault(x => x.Code.EqualsIgnoreCase(targetCurrencyCode));

        return source == null || target == null
            ? 0m
            : new Money(1m, source).ConvertTo(target).InternalAmount;
    }

    private static string BuildWarning(int excludedCount, List<string> excludedCurrencies)
    {
        if (excludedCurrencies.Count == 0)
        {
            return null;
        }

        var codes = string.Join(", ", excludedCurrencies);

        // Order/cart folds carry real counts; the top-seller fold's groups are revenue-only (count 0), hence two forms.
        return excludedCount > 0
            ? $"Excluded {excludedCount} record(s) in unconfigured currencies ({codes}) from these figures."
            : $"Excluded amounts in unconfigured currencies ({codes}) from these figures.";
    }

    private static DateTime? EarlierOf(DateTime? current, DateTime? candidate)
        => candidate != null && (current == null || candidate < current) ? candidate : current;

    private static DateTime? LaterOf(DateTime? current, DateTime? candidate)
        => candidate != null && (current == null || candidate > current) ? candidate : current;
}
