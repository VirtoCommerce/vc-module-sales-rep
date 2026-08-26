using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services.Activities;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

/// <summary>
/// The aggregation contract of <see cref="SalesRepActivityService"/> over stub sources: fan-out only to sources whose
/// categories intersect the filter, per-category counts taken from the same row fetch (Take=0 only for count-only
/// requests), and the per-category top-Skip+Take merge/sort/slice.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepActivityServiceTests
{
    private static readonly DateTime _t1 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _t2 = new(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _t3 = new(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _t4 = new(2026, 6, 4, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Search_MergesSourcesNewestFirst_AndSlices()
    {
        var orders = new StubActivitySource("orders", Event("orders", _t4), Event("orders", _t1));
        var customers = new StubActivitySource("customers", Event("customers", _t3), Event("customers", _t2));
        var service = new SalesRepActivityService([orders, customers]);

        var page1 = await service.SearchActivitiesAsync(Criteria(take: 2));
        page1.TotalCount.Should().Be(4);
        page1.Results.Select(x => x.OccurredAt).Should().Equal(_t4, _t3);

        var page2 = await service.SearchActivitiesAsync(Criteria(take: 2, skip: 2));
        page2.Results.Select(x => x.OccurredAt).Should().Equal(_t2, _t1);
    }

    [Fact]
    public async Task Search_ReturnsPerCategoryCounts_FromRowFetch()
    {
        var orders = new StubActivitySource("orders", Event("orders", _t1), Event("orders", _t2));
        var customers = new StubActivitySource("customers", Event("customers", _t3));
        var service = new SalesRepActivityService([orders, customers]);

        var result = await service.SearchActivitiesAsync(Criteria(take: 10));

        result.CategoryCounts.Select(x => (x.Category, x.Count)).Should().Equal(("orders", 2), ("customers", 1));
        result.TotalCount.Should().Be(3);
        orders.ReceivedCriteria.Should().ContainSingle(x => x.Take == 10 && x.Categories.Single() == "orders");
        customers.ReceivedCriteria.Should().ContainSingle(x => x.Take == 10 && x.Categories.Single() == "customers");
    }

    [Fact]
    public async Task Search_MultiCategorySource_CountsPerCategoryFromOwnFetch()
    {
        var analytics = new StubActivitySource(
            ["searches", "logins"],
            Event("searches", _t1), Event("searches", _t2), Event("logins", _t3));
        var service = new SalesRepActivityService([analytics]);

        var result = await service.SearchActivitiesAsync(Criteria(take: 10));

        result.CategoryCounts.Select(x => (x.Category, x.Count)).Should().Equal(("searches", 2), ("logins", 1));
        result.TotalCount.Should().Be(3);
        analytics.ReceivedCriteria.Should().HaveCount(2);
        analytics.ReceivedCriteria.Should().OnlyContain(x => x.Take == 10 && x.Categories.Count == 1);
        analytics.ReceivedCriteria.Select(x => x.Categories.Single()).Should().Equal("searches", "logins");
    }

    [Fact]
    public async Task Search_CategoryFilter_SkipsForeignSources()
    {
        var orders = new StubActivitySource("orders", Event("orders", _t1));
        var customers = new StubActivitySource("customers", Event("customers", _t2));
        var service = new SalesRepActivityService([orders, customers]);

        var result = await service.SearchActivitiesAsync(Criteria(take: 10, categories: ["customers"]));

        result.TotalCount.Should().Be(1);
        result.CategoryCounts.Select(x => x.Category).Should().Equal("customers");
        result.Results.Single().Category.Should().Be("customers");
        orders.ReceivedCriteria.Should().BeEmpty();
        customers.ReceivedCriteria.Should().ContainSingle(x => x.Take == 10);
    }

    [Fact]
    public async Task Search_TakeZero_ReturnsCountsWithoutItems_ViaTakeZero()
    {
        var orders = new StubActivitySource("orders", Event("orders", _t1));
        var service = new SalesRepActivityService([orders]);

        var result = await service.SearchActivitiesAsync(Criteria(take: 0));

        result.TotalCount.Should().Be(1);
        result.CategoryCounts.Select(x => (x.Category, x.Count)).Should().Equal(("orders", 1));
        result.Results.Should().BeEmpty();
        orders.ReceivedCriteria.Should().ContainSingle(x => x.Take == 0 && x.Skip == 0);
    }

    [Fact]
    public async Task Search_SourcesFetchTopSkipPlusTake()
    {
        var orders = new StubActivitySource("orders", Event("orders", _t1));
        var service = new SalesRepActivityService([orders]);

        await service.SearchActivitiesAsync(Criteria(take: 5, skip: 15));

        orders.ReceivedCriteria.Should().ContainSingle(x => x.Take == 20 && x.Skip == 0);
    }

    private static SalesRepActivitySearchCriteria Criteria(int take, int skip = 0, IList<string> categories = null)
        => new()
        {
            SalesRepUserId = "rep-user",
            OrganizationIds = ["org-1"],
            Categories = categories,
            Take = take,
            Skip = skip,
        };

    private static SalesRepActivityEvent Event(string category, DateTime occurredAt)
        => new() { Category = category, Type = category, OccurredAt = occurredAt, Precision = "exact" };

    private sealed class StubActivitySource : ISalesRepActivitySource
    {
        private readonly List<SalesRepActivityEvent> _events;

        public StubActivitySource(string category, params SalesRepActivityEvent[] events)
            : this([category], events)
        {
        }

        public StubActivitySource(IList<string> categories, params SalesRepActivityEvent[] events)
        {
            Categories = categories;
            _events = [.. events];
        }

        public IList<string> Categories { get; }

        public List<SalesRepActivitySearchCriteria> ReceivedCriteria { get; } = [];

        public Task<SalesRepActivitySearchResult> SearchAsync(SalesRepActivitySearchCriteria criteria)
        {
            ReceivedCriteria.Add(criteria);

            var matches = _events
                .Where(x => criteria.Categories == null || criteria.Categories.Contains(x.Category))
                .OrderByDescending(x => x.OccurredAt)
                .ToList();

            var result = new SalesRepActivitySearchResult
            {
                TotalCount = matches.Count,
                Results = matches.Skip(criteria.Skip).Take(Math.Max(criteria.Take, 0)).ToList(),
            };
            return Task.FromResult(result);
        }
    }
}
