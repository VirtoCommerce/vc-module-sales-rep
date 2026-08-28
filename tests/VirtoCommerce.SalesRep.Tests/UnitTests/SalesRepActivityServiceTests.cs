using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services.Activities;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The aggregation contract of <see cref="SalesRepActivityService"/> over stub sources: no source runs without a
/// resolved rep scope, every registered category is counted regardless of the filter (the storefront's tab badges),
/// rows and the total cover the requested categories only, a requested category's count comes from its own row fetch
/// (Take=0 only for the categories not fetched), and the two paging shapes: one fetched category pages natively,
/// several are merged, sorted and sliced from a shared fetch window.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepActivityServiceTests
{
    // What a merged view is asked for past its first page.
    private const int FetchWindow = ModuleConstants.Activities.PagingWindowBucket;

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
    public async Task Search_WithoutOrganizationScope_FailsClosed()
    {
        // The stub ignores scope entirely, exactly like a contributed source that forgets its own guard would:
        // the aggregator must not dispatch to it at all.
        var orders = new StubActivitySource("orders", Event("orders", _t1));
        var service = new SalesRepActivityService([orders]);

        var criteria = Criteria(take: 10);
        criteria.OrganizationIds = [];

        var result = await service.SearchActivitiesAsync(criteria);

        orders.ReceivedCriteria.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Results.Should().BeEmpty();
        result.CategoryCounts.Should().BeEmpty();
    }

    [Fact]
    public void Clone_DeepCopiesCollections()
    {
        // The aggregator mutates Categories on each clone before firing the per-category reads concurrently.
        var criteria = Criteria(take: 10, categories: ["orders"]);

        var clone = criteria.CloneTyped();
        clone.Categories.Add("customers");
        clone.OrganizationIds.Add("org-2");

        criteria.Categories.Should().Equal("orders");
        criteria.OrganizationIds.Should().Equal("org-1");
        clone.Take.Should().Be(10);
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
    public async Task Search_CategoryFilter_CountsEveryCategory_ButFetchesOnlyRequested()
    {
        var orders = new StubActivitySource("orders", Event("orders", _t1), Event("orders", _t4));
        var analytics = new StubActivitySource(
            ["searches", "logins"],
            Event("searches", _t2), Event("logins", _t3));
        var service = new SalesRepActivityService([orders, analytics]);

        var result = await service.SearchActivitiesAsync(Criteria(take: 10, categories: ["logins"]));

        // Every tab badge keeps its own unfiltered total while one tab is selected.
        result.CategoryCounts.Select(x => (x.Category, x.Count)).Should().Equal(("orders", 2), ("searches", 1), ("logins", 1));
        // The pager is per-tab: rows and total cover the selected category only.
        result.TotalCount.Should().Be(1);
        result.Results.Should().ContainSingle().Which.Category.Should().Be("logins");

        // The fetched category reuses its row fetch; the rest are counted with Take=0.
        analytics.ReceivedCriteria.Should().HaveCount(2);
        analytics.ReceivedCriteria.Should().ContainSingle(x => x.Categories.Single() == "logins" && x.Take == 10 && x.Skip == 0);
        analytics.ReceivedCriteria.Should().ContainSingle(x => x.Categories.Single() == "searches" && x.Take == 0);
        orders.ReceivedCriteria.Should().ContainSingle(x => x.Categories.Single() == "orders" && x.Take == 0 && x.Skip == 0);
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
    public async Task Search_OneFetchedCategory_PagesNatively()
    {
        var orders = new StubActivitySource("orders", Event("orders", _t1), Event("orders", _t2), Event("orders", _t3));
        var analytics = new StubActivitySource(["searches"], Event("searches", _t4));
        var service = new SalesRepActivityService([orders, analytics]);

        // Nothing to merge with, so the source is asked for the page itself rather than everything above it.
        var result = await service.SearchActivitiesAsync(Criteria(take: 1, skip: 1, categories: ["orders"]));

        orders.ReceivedCriteria.Should().ContainSingle(x => x.Take == 1 && x.Skip == 1);
        result.Results.Should().ContainSingle().Which.OccurredAt.Should().Be(_t2);
    }

    [Fact]
    public async Task Search_MergedFirstPage_IsAskedForExactly()
    {
        var orders = new StubActivitySource("orders", Event("orders", _t1));
        var customers = new StubActivitySource("customers", Event("customers", _t2));
        var service = new SalesRepActivityService([orders, customers]);

        // The commonest request of all (every dashboard widget): rounding it up would make the cheapest read the
        // most expensive one.
        await service.SearchActivitiesAsync(Criteria(take: 5));

        orders.ReceivedCriteria.Should().OnlyContain(x => x.Take == 5 && x.Skip == 0);
        customers.ReceivedCriteria.Should().OnlyContain(x => x.Take == 5 && x.Skip == 0);
    }

    [Fact]
    public async Task Search_MergedDeeperPages_ShareOneWindow()
    {
        var orders = new StubActivitySource("orders", Event("orders", _t1));
        var customers = new StubActivitySource("customers", Event("customers", _t2));
        var service = new SalesRepActivityService([orders, customers]);

        // A merged page can only be sliced from the top Skip+Take rows of every category, but two pages inside the
        // same bucket ask the sources the same question — so the second costs them nothing beyond the first.
        await service.SearchActivitiesAsync(Criteria(take: 5, skip: 15));
        await service.SearchActivitiesAsync(Criteria(take: 5, skip: 25));

        orders.ReceivedCriteria.Should().HaveCount(2);
        orders.ReceivedCriteria.Should().OnlyContain(x => x.Take == FetchWindow && x.Skip == 0);
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
