using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Modules;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Data.Services.Activities;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The paging contract of <see cref="AnalyticsSalesRepActivitySource"/>: a first page reads at least a default page,
/// so callers asking for fewer rows share that read, and still gets back only the rows it asked for.
/// </summary>
[Trait("Category", "Unit")]
public class AnalyticsSalesRepActivitySourceTests
{
    private static readonly DateTime _feb = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Search_FirstPage_ReadsADefaultPage_ReturnsTheRowsAskedFor(int take)
    {
        var analytics = new TestAnalytics();
        for (var hour = 0; hour < 8; hour++)
        {
            analytics.AddEvent(AnalyticsConstants.EventNames.Login, _feb.AddHours(hour), count: 2, "org-1");
        }

        var result = await CreateSource(analytics.CreateService()).SearchAsync(CreateCriteria(take, skip: 0));

        result.Results.Should().HaveCount(take);
        result.TotalCount.Should().Be(8);
        result.EventCount.Should().Be(16);
        analytics.ReceivedQueries.Should().ContainSingle(x => x.Take == SalesRepActivitySearchCriteria.DefaultTake && x.Skip == 0);
    }

    [Fact]
    public async Task Search_LaterPage_ReadsThePageAskedFor()
    {
        var analytics = new TestAnalytics();

        await CreateSource(analytics.CreateService()).SearchAsync(CreateCriteria(take: 5, skip: 5));

        analytics.ReceivedQueries.Should().ContainSingle(x => x.Take == 5 && x.Skip == 5);
    }

    private static AnalyticsSalesRepActivitySource CreateSource(IAnalyticsService analyticsService)
    {
        var services = new ServiceCollection().AddSingleton(analyticsService).BuildServiceProvider();

        return new AnalyticsSalesRepActivitySource(
            new OptionalDependencyManager<IAnalyticsService>(services),
            new SalesRepAnalyticsScope(),
            NullLogger<AnalyticsSalesRepActivitySource>.Instance);
    }

    private static SalesRepActivitySearchCriteria CreateCriteria(int take, int skip)
    {
        return new SalesRepActivitySearchCriteria
        {
            StoreId = "B2B-store",
            OrganizationIds = ["org-1"],
            Categories = [ModuleConstants.Activities.Categories.Logins],
            Take = take,
            Skip = skip,
        };
    }
}
