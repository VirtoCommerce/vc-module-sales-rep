using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// The analytics scope is one service: a project that overrides it once changes the scope of every rep-facing read,
/// so no reader can keep the old one.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepAnalyticsScopeComponentTests
{
    [Fact]
    public async Task OverriddenAudience_ReachesEveryRead()
    {
        var analytics = new TestAnalytics();
        using var ctx = SalesRepTestContext.Create(services =>
        {
            analytics.Register(services);
            services.AddTransient<ISalesRepAnalyticsScope, ChannelAnalyticsScope>();
        });
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        await ctx.ExecuteGraphQlAsync(
            "query { salesRepActivities { totalCount } }",
            userId: rep.UserId);
        var feedReads = analytics.ReceivedQueries.Count;

        await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerActivitySummary(organizationId: \"org-1\") { visitsCount lastSearchTerm lastViewedProduct { code } } }",
            userId: rep.UserId);
        var summaryReads = analytics.ReceivedQueries.Count - feedReads;

        await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerInsights { searchTerms { term } browsedProducts { sku } } }",
            userId: rep.UserId);
        var insightsReads = analytics.ReceivedQueries.Count - feedReads - summaryReads;

        // Each surface reads, so the assertion below covers all three rather than whichever happened to run.
        feedReads.Should().BePositive();
        summaryReads.Should().BePositive();
        insightsReads.Should().BePositive();
        analytics.ReceivedQueries.Should().OnlyContain(x => x.DimensionFilters.Any(filter =>
            filter.DimensionName == ChannelAnalyticsScope.ChannelDimension &&
            filter.Values.Single() == ChannelAnalyticsScope.WebChannel));
    }
}
