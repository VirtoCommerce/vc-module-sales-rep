using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Google.Analytics.Data.V1Beta;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Services;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Tests.Infrastructure;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;
using DataApiSettings = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.Settings.DataApi;
using SalesRepConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Google's Analytics Data API, which no test can call: the analytics module's report client, answered from a seeded
/// event pool the way GA4 answers a report. Everything above it is the module's own code — the analytics service and
/// its cache, the data source that builds the report request and maps the response, the settings resolver that reads
/// the property from the store — so a change there shows here, as it did not while a hand-written data source stood in
/// for all of it.
/// </summary>
/// <remarks>
/// A report is answered as GA4 answers one: the rows matching the date range (whole days, inclusive) and the dimension
/// filter (an in-list filter, or an AND of them), aggregated by the requested dimensions with the metric summed,
/// ordered by the requested order, paged by limit and offset, with the row count and the metric's total over every
/// row. A dimension a row lacks reads "(not set)", and so does the hour of an event seeded without one.
/// </remarks>
internal sealed class TestAnalytics : IGoogleAnalyticsReportClient
{
    /// <summary>The property every store falls back to: the platform-level setting.</summary>
    public const string PlatformPropertyId = "test-property";

    private const string NotSet = "(not set)";

    /// <summary>GA4's first day, which the data source asks from when a read has no start.</summary>
    private const string UnboundedStartDate = "2015-08-14";

    /// <summary>
    /// Whether the analytics module is configured: true saves the platform's property and each store's own, as an
    /// administrator does; false leaves them empty, a store with analytics not configured.
    /// </summary>
    public bool Configured { get; set; } = true;

    /// <summary>When set, every Google call fails with it — a refused credential, a rejected property, a 429.</summary>
    public Exception FailWith { get; set; }

    public List<AnalyticsEvent> Events { get; } = [];

    /// <summary>
    /// Every report request that reached Google: one per search, and one per named event of a summary read. Concurrent
    /// because those are — the real service runs them through Parallel.ForEachAsync.
    /// </summary>
    public ConcurrentQueue<RunReportRequest> ReceivedRequests { get; } = new();

    /// <summary>
    /// The requests read back in the module's own terms: the page (limit and offset; the data source asks for one row
    /// when it wants none), the order (by count, or by hour), the dimensions asked for, the event names and the
    /// dimension filters the request narrows to.
    /// </summary>
    public IList<AnalyticsDataQuery> ReceivedQueries => ReceivedRequests.Select(ToQuery).ToList();

    /// <summary>
    /// The stores whose property the requests read (each store has a property of its own, see <see cref="ConfigureAsync"/>);
    /// null for the platform's.
    /// </summary>
    public IList<string> ReceivedStoreIds => ReceivedRequests.Select(x => StoreIdOf(PropertyName.Parse(x.Property).PropertyId)).ToList();

    /// <summary>
    /// Hosts the analytics module, as its Module.cs registers it, with this in place of the report client that calls
    /// Google. The harness then configures it (<see cref="ConfigureAsync"/>).
    /// </summary>
    public void Register(IServiceCollection services)
    {
        services.AddGoogleAnalyticsModule();
        services.AddSingleton(this);
        services.AddSingleton<IGoogleAnalyticsReportClient>(this);
    }

    /// <summary>
    /// Saves the module's property settings when <see cref="Configured"/>, as an administrator configures them: the
    /// platform's property, and one per store so a request shows whose it read.
    /// </summary>
    public async Task ConfigureAsync(SalesRepTestContext context, IEnumerable<string> storeIds)
    {
        if (!Configured)
        {
            return;
        }

        await context.SetSettingAsync(DataApiSettings.PropertyId, PlatformPropertyId);

        foreach (var storeId in storeIds)
        {
            await context.SaveStoreAsync(storeId, store => store.Settings.Single(x => x.Name == DataApiSettings.PropertyId.Name).Value = PropertyIdOf(storeId));
        }
    }

    /// <summary>
    /// The real analytics service over the real data source and this, for a unit test of a module service that has no
    /// container: its store's property comes from a resolver that names one per store, as <see cref="ConfigureAsync"/>
    /// saves them (a unit test has no store to read it from).
    /// </summary>
    public IAnalyticsService CreateService()
    {
        var platformMemoryCache = new PlatformMemoryCache(
            new MemoryCache(Options.Create(new MemoryCacheOptions())),
            Options.Create(new CachingOptions()),
            NullLogger<PlatformMemoryCache>.Instance);

        return new AnalyticsService(
            new StorePropertySettings(this),
            platformMemoryCache,
            new GoogleAnalyticsDataSource(this, NullLogger<GoogleAnalyticsDataSource>.Instance),
            NullLogger<AnalyticsService>.Instance);
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
        // A customer's by default; a rep's own browsing is seeded by passing the dimension, which overrides this.
        analyticsEvent.Dimensions[AnalyticsConstants.UserDimensions.IsSalesRep] = SalesRepConstants.Analytics.IsSalesRepValues.Customer;

        foreach (var (name, value) in dimensions)
        {
            analyticsEvent.Dimensions[name] = value;
        }

        Events.Add(analyticsEvent);
    }

    public Task<RunReportResponse> RunReportAsync(RunReportRequest request, TimeSpan timeout)
    {
        ReceivedRequests.Enqueue(request);

        if (FailWith != null)
        {
            return Task.FromException<RunReportResponse>(FailWith);
        }

        var dimensionNames = request.Dimensions.Select(x => x.Name).ToList();
        var dateRange = request.DateRanges.Single();
        var from = ParseDate(dateRange.StartDate);
        var to = ParseDate(dateRange.EndDate) ?? DateTime.UtcNow.Date;

        // GA4 aggregates the matching events by the requested dimensions, summing the metric.
        var rows = Events
            .Where(x => x.OccurredAt == null || (x.OccurredAt >= from && x.OccurredAt < to.AddDays(1)))
            .Where(x => request.DimensionFilter == null || Matches(request.DimensionFilter, x))
            .GroupBy(x => string.Join("\u001f", dimensionNames.Select(name => ValueOf(x, name))))
            .Select(group => (Values: dimensionNames.Select(name => ValueOf(group.First(), name)).ToList(), Count: group.Sum(x => x.Count)))
            .ToList();

        var orderBy = request.OrderBys.SingleOrDefault();
        rows = orderBy?.Metric != null
            ? [.. rows.OrderByDescending(x => x.Count).ThenBy(x => string.Join(",", x.Values), StringComparer.Ordinal)]
            : orderBy?.Dimension != null
                ? [.. rows.OrderByDescending(x => x.Values[dimensionNames.IndexOf(orderBy.Dimension.DimensionName)], StringComparer.Ordinal)]
                : rows;

        var response = new RunReportResponse
        {
            RowCount = rows.Count,
            Metadata = new ResponseMetaData { TimeZone = "Etc/UTC" },
        };
        response.DimensionHeaders.AddRange(dimensionNames.Select(x => new DimensionHeader { Name = x }));
        response.MetricHeaders.AddRange(request.Metrics.Select(x => new MetricHeader { Name = x.Name }));

        if (request.MetricAggregations.Contains(MetricAggregation.Total) && rows.Count > 0)
        {
            var total = new Row();
            total.MetricValues.Add(new MetricValue { Value = rows.Sum(x => x.Count).ToString(CultureInfo.InvariantCulture) });
            response.Totals.Add(total);
        }

        foreach (var (values, count) in rows.Skip((int)request.Offset).Take((int)request.Limit))
        {
            var row = new Row();
            row.DimensionValues.AddRange(values.Select(x => new DimensionValue { Value = x }));
            row.MetricValues.Add(new MetricValue { Value = count.ToString(CultureInfo.InvariantCulture) });
            response.Rows.Add(row);
        }

        return Task.FromResult(response);
    }

    public Task ValidateCredentialAsync() => Task.CompletedTask;

    public Task<Metadata> GetMetadataAsync(string propertyId, TimeSpan timeout) => throw new NotSupportedException("No test reads GA's metadata.");

    public Task<RunRealtimeReportResponse> RunRealtimeReportAsync(RunRealtimeReportRequest request, TimeSpan timeout) => throw new NotSupportedException("No test reads a realtime report.");

    public Task<CheckCompatibilityResponse> CheckCompatibilityAsync(CheckCompatibilityRequest request, TimeSpan timeout) => throw new NotSupportedException("No test checks GA's compatibility.");

    /// <summary>The property id a store's own setting names.</summary>
    public static string PropertyIdOf(string storeId) => $"property-{storeId}";

    private static string StoreIdOf(string propertyId)
        => propertyId.StartsWith("property-", StringComparison.Ordinal) ? propertyId["property-".Length..] : null;

    // GA's date range is whole days, inclusive at both ends: the request carries yyyy-MM-dd, so an event at any hour
    // of the end day is in range. The data source asks from GA's first day and up to "today" when unbounded.
    private static DateTime? ParseDate(string value)
        => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;

    private static bool Matches(FilterExpression expression, AnalyticsEvent analyticsEvent)
    {
        return expression.ExprCase switch
        {
            FilterExpression.ExprOneofCase.AndGroup => expression.AndGroup.Expressions.All(x => Matches(x, analyticsEvent)),
            FilterExpression.ExprOneofCase.Filter when expression.Filter.InListFilter != null
                => expression.Filter.InListFilter.Values.Contains(ValueOf(analyticsEvent, expression.Filter.FieldName)),
            _ => throw new NotSupportedException($"The stand-in for GA answers in-list filters and their AND only, not {expression.ExprCase}."),
        };
    }

    // A custom user dimension is requested by its "customUser:" name and seeded by its own.
    private static string ValueOf(AnalyticsEvent analyticsEvent, string dimensionName)
    {
        if (dimensionName == AnalyticsConstants.Dimensions.EventName)
        {
            return analyticsEvent.EventName;
        }

        if (dimensionName == AnalyticsConstants.Dimensions.DateHour)
        {
            return analyticsEvent.OccurredAt?.ToUniversalTime().ToString("yyyyMMddHH", CultureInfo.InvariantCulture) ?? NotSet;
        }

        return analyticsEvent.Dimensions.TryGetValue(Unprefixed(dimensionName), out var value) && !string.IsNullOrEmpty(value) ? value : NotSet;
    }

    private static AnalyticsDataQuery ToQuery(RunReportRequest request)
    {
        var query = AbstractTypeFactory<AnalyticsDataQuery>.TryCreateInstance();
        query.PropertyId = PropertyName.Parse(request.Property).PropertyId;
        query.Take = (int)request.Limit;
        query.Skip = (int)request.Offset;
        query.SortBy = request.OrderBys.SingleOrDefault()?.Metric != null ? AnalyticsConstants.SortBy.Count : AnalyticsConstants.SortBy.Date;
        // The data source asks from GA's first day ("2015-08-14") and up to "today" when the read is unbounded.
        var dateRange = request.DateRanges.Single();
        query.From = dateRange.StartDate == UnboundedStartDate ? null : ParseDate(dateRange.StartDate);
        query.To = ParseDate(dateRange.EndDate);
        query.DimensionNames = request.Dimensions
            .Select(x => x.Name)
            .Where(x => x != AnalyticsConstants.Dimensions.EventName && x != AnalyticsConstants.Dimensions.DateHour)
            .Select(Unprefixed)
            .ToList();

        var filters = request.DimensionFilter == null
            ? []
            : request.DimensionFilter.ExprCase == FilterExpression.ExprOneofCase.AndGroup
                ? request.DimensionFilter.AndGroup.Expressions.Select(x => x.Filter).ToList()
                : [request.DimensionFilter.Filter];

        query.EventNames = filters
            .Where(x => x.FieldName == AnalyticsConstants.Dimensions.EventName)
            .SelectMany(x => x.InListFilter.Values)
            .ToList();
        query.DimensionFilters = filters
            .Where(x => x.FieldName != AnalyticsConstants.Dimensions.EventName)
            .Select(x => new AnalyticsDimensionFilter
            {
                DimensionName = Unprefixed(x.FieldName),
                Values = [.. x.InListFilter.Values],
            })
            .ToList();

        return query;
    }

    // A custom user dimension's request name carries "customUser:"; the module's own name doesn't.
    private static string Unprefixed(string dimensionName)
        => dimensionName.StartsWith(AnalyticsConstants.UserDimensions.Prefix, StringComparison.Ordinal)
            ? dimensionName[AnalyticsConstants.UserDimensions.Prefix.Length..]
            : dimensionName;

    /// <summary>A unit test's stand-in for the store's settings: each store's own property, as configured.</summary>
    private sealed class StorePropertySettings(TestAnalytics analytics) : IAnalyticsSettingsResolver
    {
        public Task<AnalyticsDataApiSettings> ResolveAsync(string storeId)
        {
            var settings = AbstractTypeFactory<AnalyticsDataApiSettings>.TryCreateInstance();
            settings.PropertyId = !analytics.Configured ? null : storeId == null ? PlatformPropertyId : PropertyIdOf(storeId);
            settings.CacheTtlMinutes = (int)DataApiSettings.CacheTtlMinutes.DefaultValue;
            settings.RequestTimeoutSeconds = (int)DataApiSettings.RequestTimeoutSeconds.DefaultValue;

            return Task.FromResult(settings);
        }
    }
}
