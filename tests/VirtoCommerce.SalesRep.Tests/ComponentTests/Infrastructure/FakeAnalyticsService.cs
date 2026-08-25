using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// In-memory <see cref="IAnalyticsService"/>: serves a seeded event pool the way GA4 would — honoring event-name,
/// dimension and date filters, newest-first, Take=0 returning counts only — and records every criteria received so
/// tests can assert the sales-rep layer's scoping (session_kind=self + organization_id) directly.
/// </summary>
internal sealed class FakeAnalyticsService : IAnalyticsService
{
    public bool Configured { get; set; } = true;

    public List<AnalyticsEvent> Events { get; } = [];

    public List<AnalyticsEventSearchCriteria> ReceivedSearchCriteria { get; } = [];

    public List<AnalyticsEventSummaryCriteria> ReceivedSummaryCriteria { get; } = [];

    public void AddEvent(string eventName, DateTime occurredAt, int count, string organizationId,
        string sessionKind = AnalyticsConstants.SessionKinds.Self, params (string Name, string Value)[] dimensions)
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

    public Task<bool> IsConfiguredAsync(string storeId) => Task.FromResult(Configured);

    public Task<AnalyticsEventSearchResult> SearchEventsAsync(AnalyticsEventSearchCriteria criteria)
    {
        ReceivedSearchCriteria.Add(criteria);

        var matches = Filter(criteria)
            .OrderByDescending(x => x.OccurredAt)
            .ToList();

        if (AnalyticsConstants.SortBy.Count.EqualsIgnoreCase(criteria.SortBy))
        {
            matches = AggregateByDimensionTuple(matches, criteria.DimensionNames);
        }

        var result = new AnalyticsEventSearchResult
        {
            TotalCount = matches.Count,
            Events = criteria.Take > 0
                ? matches.Skip(criteria.Skip).Take(criteria.Take).Select(x => x.CloneTyped()).ToList()
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

    public Task<IList<AnalyticsEventSummary>> GetEventSummariesAsync(AnalyticsEventSummaryCriteria criteria)
    {
        ReceivedSummaryCriteria.Add(criteria);

        var groups = Filter(criteria)
            .GroupBy(x => x.EventName)
            .ToDictionary(x => x.Key, x => x.ToList());

        IList<AnalyticsEventSummary> result = (criteria.EventNames ?? [])
            .Select(eventName =>
            {
                var summary = new AnalyticsEventSummary { EventName = eventName };
                if (groups.TryGetValue(eventName, out var group))
                {
                    summary.TotalCount = group.Sum(x => x.Count);
                    summary.LastOccurredAt = group.Max(x => x.OccurredAt);
                }

                return summary;
            })
            .ToList();
        return Task.FromResult(result);
    }

    private IEnumerable<AnalyticsEvent> Filter(AnalyticsEventCriteriaBase criteria)
    {
        return Events
            .Where(x => criteria.EventNames.IsNullOrEmpty() || criteria.EventNames.Contains(x.EventName))
            .Where(x => criteria.From == null || x.OccurredAt >= criteria.From)
            .Where(x => criteria.To == null || x.OccurredAt <= criteria.To)
            .Where(x => (criteria.DimensionFilters ?? []).All(filter =>
                x.Dimensions.TryGetValue(filter.DimensionName, out var value) && filter.Values.Contains(value)));
    }
}
