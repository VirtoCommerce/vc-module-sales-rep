using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;
using SalesRepConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// The analytics module as a consumer actually meets it: the REAL <see cref="AnalyticsService"/> — its cache, its
/// argument/configuration/call ordering, its summary shape and its failure contract — over a seeded event pool
/// standing in for Google.
///
/// Only the module's own two provider seams are doubled. A hand-written <see cref="IAnalyticsService"/> could not
/// do this: it re-stated the module's behaviour instead of running it, so a contract change (reads that throw, a
/// store id that stopped being optional) passed straight through a green suite.
/// </summary>
internal sealed class TestAnalytics : IAnalyticsDataSource, IAnalyticsSettingsResolver
{
    private const string TestPropertyId = "test-property";

    /// <summary>Whether a property id resolves at all. False models a store with analytics not configured.</summary>
    public bool Configured { get; set; } = true;

    /// <summary>When set, every Google call fails with it — a refused credential, a rejected property, a 429.</summary>
    public Exception FailWith { get; set; }

    public List<AnalyticsEvent> Events { get; } = [];

    /// <summary>
    /// Every query that reached the provider: one per search, and 1 + N per summary read (the count-mode totals
    /// plus a newest-bucket probe for each event name that has any).
    ///
    /// Concurrent because the summary probes are: the real service runs them through Parallel.ForEachAsync, so a
    /// plain List here would be written from several threads at once the moment a read names two event names.
    /// </summary>
    public ConcurrentQueue<AnalyticsDataQuery> ReceivedQueries { get; } = new();

    /// <summary>Every store id the module resolved settings for — including the absent one.</summary>
    public ConcurrentQueue<string> ReceivedStoreIds { get; } = new();

    /// <summary>Wires the real service over these two seams, the way the analytics module's own Module.cs does.</summary>
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<IAnalyticsDataSource>(this);
        services.AddSingleton<IAnalyticsSettingsResolver>(this);
        services.AddSingleton<IAnalyticsService>(sp => new AnalyticsService(
            sp.GetRequiredService<IAnalyticsSettingsResolver>(),
            sp.GetRequiredService<IPlatformMemoryCache>(),
            sp.GetRequiredService<IAnalyticsDataSource>(),
            NullLogger<AnalyticsService>.Instance));
    }

    /// <summary>The same real service, for a unit test that has no container to register into.</summary>
    public IAnalyticsService CreateService()
    {
        var platformMemoryCache = new PlatformMemoryCache(
            new MemoryCache(Options.Create(new MemoryCacheOptions())),
            Options.Create(new CachingOptions()),
            NullLogger<PlatformMemoryCache>.Instance);

        return new AnalyticsService(this, platformMemoryCache, this, NullLogger<AnalyticsService>.Instance);
    }

    // occurredAt is nullable so a test can seed the row shape GA returns when the hour bucket is "(not set)".
    public void AddEvent(string eventName, DateTime? occurredAt, int count, string organizationId,
        string sessionKind = SalesRepConstants.Analytics.SessionKinds.Self, params (string Name, string Value)[] dimensions)
    {
        var analyticsEvent = new AnalyticsEvent
        {
            EventName = eventName,
            OccurredAt = occurredAt,
            Count = count,
        };
        analyticsEvent.Dimensions[AnalyticsConstants.UserDimensions.OrganizationId] = organizationId;
        analyticsEvent.Dimensions[AnalyticsConstants.UserDimensions.SessionKind] = sessionKind;

        foreach (var (name, value) in dimensions)
        {
            analyticsEvent.Dimensions[name] = value;
        }

        Events.Add(analyticsEvent);
    }

    public Task<AnalyticsDataApiSettings> ResolveAsync(string storeId)
    {
        ReceivedStoreIds.Enqueue(storeId);

        return Task.FromResult(new AnalyticsDataApiSettings
        {
            PropertyId = Configured ? TestPropertyId : null,
        });
    }

    public Task<AnalyticsEventSearchResult> GetRowsAsync(AnalyticsDataQuery query)
    {
        ReceivedQueries.Enqueue(query);

        if (FailWith != null)
        {
            return Task.FromException<AnalyticsEventSearchResult>(FailWith);
        }

        var matches = Filter(query)
            .OrderByDescending(x => x.OccurredAt)
            .ToList();

        if (AnalyticsConstants.SortBy.Count.EqualsIgnoreCase(query.SortBy))
        {
            matches = AggregateByDimensionTuple(matches, query.DimensionNames);
        }

        var result = new AnalyticsEventSearchResult
        {
            TotalCount = matches.Count,
            Events = query.Take > 0
                ? matches.Skip(query.Skip).Take(query.Take).Select(x => x.CloneTyped()).ToList()
                : [],
        };
        return Task.FromResult(result);
    }

    // Mirrors the GA count sort: no dateHour dimension, so GA collapses the hour buckets into one row per
    // (eventName, requested-dimension tuple) — count summed, OccurredAt null, ordered by count desc then tuple.
    private static List<AnalyticsEvent> AggregateByDimensionTuple(List<AnalyticsEvent> events, IList<string> dimensionNames)
    {
        return events
            .GroupBy(x => GetDimensionTupleKey(x, dimensionNames))
            .Select(group =>
            {
                var first = group.First();
                var aggregated = new AnalyticsEvent
                {
                    EventName = first.EventName,
                    Count = group.Sum(x => x.Count),
                };

                foreach (var name in dimensionNames ?? [])
                {
                    if (first.Dimensions.TryGetValue(name, out var value))
                    {
                        aggregated.Dimensions[name] = value;
                    }
                }

                return aggregated;
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => GetDimensionTupleKey(x, dimensionNames), StringComparer.Ordinal)
            .ToList();
    }

    private static string GetDimensionTupleKey(AnalyticsEvent analyticsEvent, IList<string> dimensionNames)
    {
        var dimensions = string.Join(",", (dimensionNames ?? [])
            .OrderBy(x => x, StringComparer.Ordinal)
            .Select(x => $"{x}={(analyticsEvent.Dimensions.TryGetValue(x, out var value) ? value : null)}"));

        return $"{analyticsEvent.EventName}|{dimensions}";
    }

    // A GA date range is whole days, inclusive at both ends: the request carries yyyy-MM-dd, so an event at any
    // hour of the To day is in range. Comparing the raw timestamps instead would drop the current day.
    private IEnumerable<AnalyticsEvent> Filter(AnalyticsDataQuery query)
    {
        return Events
            .Where(x => query.EventNames.IsNullOrEmpty() || query.EventNames.Contains(x.EventName))
            .Where(x => query.From == null || x.OccurredAt >= query.From.Value.Date)
            .Where(x => query.To == null || x.OccurredAt < query.To.Value.Date.AddDays(1))
            .Where(x => (query.DimensionFilters ?? []).All(filter =>
                x.Dimensions.TryGetValue(filter.DimensionName, out var value) && filter.Values.Contains(value)));
    }
}
