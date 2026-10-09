using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services.Statistics;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

// The service is public, so a caller can hand it an empty list: that has to match nothing, never fall through to an
// unfiltered read.
[Trait("Category", "Component")]
public class SalesRepCustomerOrderStatisticsServiceTests
{
    private static readonly DateTime _feb2026 = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EmptyOrganizationList_MatchesNothing()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepWithOrderAsync(ctx);
        var service = ctx.GetRequiredService<ICustomerOrderStatisticsService>();

        var served = await service.GetStatisticsAsync(Criteria(rep.UserId, organizationIds: ["org-1"], statuses: null));
        served.Count.Should().Be(1);
        served.Total.Should().Be(100m);

        var none = await service.GetStatisticsAsync(Criteria(rep.UserId, organizationIds: [], statuses: null));
        none.Count.Should().Be(0);
        none.Total.Should().Be(0m);
    }

    [Fact]
    public async Task EmptyStatusList_MatchesNothing()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepWithOrderAsync(ctx);
        var service = ctx.GetRequiredService<ICustomerOrderStatisticsService>();

        var matching = await service.GetStatisticsAsync(Criteria(rep.UserId, organizationIds: ["org-1"], statuses: ["New"]));
        matching.Count.Should().Be(1);
        matching.Total.Should().Be(100m);

        var none = await service.GetStatisticsAsync(Criteria(rep.UserId, organizationIds: ["org-1"], statuses: []));
        none.Count.Should().Be(0);
        none.Total.Should().Be(0m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task EmptyCustomerId_IsRefused(string customerId)
    {
        using var ctx = SalesRepTestContext.Create();
        await SeedRepWithOrderAsync(ctx);
        var service = ctx.GetRequiredService<ICustomerOrderStatisticsService>();

        var read = () => service.GetStatisticsAsync(Criteria(customerId, organizationIds: ["org-1"], statuses: null));

        await read.Should().ThrowAsync<ArgumentException>();
    }

    private static async Task<SalesRepDetails> SeedRepWithOrderAsync(SalesRepTestContext ctx)
    {
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        OrderSeeder.Seed(ctx, id: "o1", org: "org-1", number: "ORD-1", createdDate: _feb2026, status: "New", total: 100m);

        return rep;
    }

    private static CustomerOrderStatisticsCriteria Criteria(string customerId, IList<string> organizationIds, IList<string> statuses)
    {
        var criteria = AbstractTypeFactory<CustomerOrderStatisticsCriteria>.TryCreateInstance();
        criteria.CustomerId = customerId;
        criteria.OrganizationIds = organizationIds;
        criteria.Statuses = statuses;
        criteria.CurrencyCode = "USD";

        return criteria;
    }
}
