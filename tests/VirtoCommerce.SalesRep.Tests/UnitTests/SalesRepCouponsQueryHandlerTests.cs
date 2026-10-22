using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.MarketingModule.Core.Model.Promotions.Search;
using VirtoCommerce.MarketingModule.Core.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Queries;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The salesRepCoupons contract over stub marketing services: the promotion set matches xAPI promotionCoupons,
/// expired, used-up (counted fresh, not from the cached coupon) and member-only coupons are dropped BEFORE paging (so
/// totalCount and pages stay exact), the card's endDate is the earlier of the coupon's expiration and the promotion's
/// end, and a missing module or a non-rep caller reads empty.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepCouponsQueryHandlerTests
{
    private const string StoreId = "B2B-store";

    private static readonly DateTime _future = DateTime.UtcNow.AddDays(30);
    private static readonly DateTime _later = DateTime.UtcNow.AddDays(60);
    private static readonly DateTime _past = DateTime.UtcNow.AddDays(-1);

    [Fact]
    public async Task Handle_SearchesTheSamePromotionsAsXapiPromotionCoupons()
    {
        var promotions = new StubPromotionSearchService([]);

        await Handler(promotions, new StubCouponSearchService([])).Handle(Query(), CancellationToken.None);

        var criteria = promotions.ReceivedCriteria.Should().ContainSingle().Subject;
        criteria.Store.Should().Be(StoreId);
        criteria.OnlyActive.Should().BeTrue();
        criteria.IsPublic.Should().Be(true);
        criteria.CouponCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DropsExpiredAndUsedUpCoupons_AndSortsBySoonestEnd()
    {
        var handler = Handler(
            new StubPromotionSearchService([Promotion("p1", _later), Promotion("p2"), Promotion("p3"), Promotion("p4"), Promotion("p5")]),
            new StubCouponSearchService(
            [
                Coupon("c1", "p1"),
                Coupon("c2", "p2", expiration: _past),
                Coupon("c3", "p3", maxUses: 2, used: 2),
                Coupon("c4", "p4", maxUses: 5, used: 3, expiration: _future),
                Coupon("c5", "p5", maxUses: 0, used: 100),
            ]));

        var result = await handler.Handle(Query(), CancellationToken.None);

        result.TotalCount.Should().Be(3);
        // c4 ends with its own expiration (sooner), c1 with its promotion, c5 never.
        result.Results.Select(x => x.Id).Should().Equal("c4", "c1", "c5");
        result.Results[0].EndDate.Should().Be(_future);
        result.Results[1].EndDate.Should().Be(_later);
        result.Results[1].ExpirationDate.Should().BeNull();
        result.Results[1].PromotionEndDate.Should().Be(_later);
    }

    // Marketing caches a coupon with the uses count it had when it loaded, and recording a use doesn't evict it.
    [Fact]
    public async Task Handle_CountsUsesFresh_NotFromTheCachedCoupon()
    {
        var cached = new[] { Coupon("c1", "p1", maxUses: 2), Coupon("c2", "p2", maxUses: 5), Coupon("c3", "p3") };
        var handler = Handler(
            new StubPromotionSearchService([Promotion("p1"), Promotion("p2"), Promotion("p3")]),
            new StubCouponSearchService(cached),
            uses: new Dictionary<string, int> { ["CODE-c1"] = 2, ["CODE-c2"] = 3 });

        var result = await handler.Handle(Query(), CancellationToken.None);

        result.Results.Select(x => x.Id).Should().Equal("c2", "c3");
        result.Results[0].TotalUsesCount.Should().Be(3);
        cached.Should().OnlyContain(x => x.TotalUsesCount == 0, "the cached coupons must not be written to");
    }

    // Checkout accepts a member's coupon only from that contact or organization; the list isn't per customer.
    [Fact]
    public async Task Handle_DropsCouponsRegisteredToAMember()
    {
        var handler = Handler(
            new StubPromotionSearchService([Promotion("p1"), Promotion("p2")]),
            new StubCouponSearchService([Coupon("c1", "p1", memberId: "org-z"), Coupon("c2", "p2")]));

        var result = await handler.Handle(Query(), CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Results.Select(x => x.Id).Should().Equal("c2");
    }

    // Checkout evaluates only active promotions, so a coupon expiring after its promotion stops at the promotion's end.
    [Fact]
    public async Task Handle_EndDate_IsTheEarlierOfTheCouponsExpirationAndThePromotionsEnd()
    {
        var handler = Handler(
            new StubPromotionSearchService([Promotion("p1", _future), Promotion("p2", _later)]),
            new StubCouponSearchService([Coupon("c1", "p1", expiration: _later.AddDays(30)), Coupon("c2", "p2", expiration: _later.AddDays(-1))]));

        var result = await handler.Handle(Query(), CancellationToken.None);

        result.Results.Select(x => x.Id).Should().Equal("c1", "c2");
        result.Results[0].EndDate.Should().Be(_future);
        result.Results[1].EndDate.Should().Be(_later.AddDays(-1));
    }

    // The list has one fixed order and no search, so it doesn't publish arguments it would ignore.
    [Fact]
    public void Arguments_DoNotIncludeKeywordOrSort()
    {
        new SalesRepCouponsQuery().GetArguments().Select(x => x.Name).Should().Equal("StoreId", "CultureName");
    }

    [Fact]
    public async Task Handle_PagesAfterFiltering()
    {
        var promotionList = Enumerable.Range(1, 5).Select(i => Promotion($"p{i}")).ToArray();
        var couponList = Enumerable.Range(1, 5).Select(i => Coupon($"c{i}", $"p{i}")).Append(Coupon("c0", "p1", expiration: _past)).ToArray();
        var handler = Handler(new StubPromotionSearchService(promotionList), new StubCouponSearchService(couponList));

        var page2 = await handler.Handle(Query(skip: 2, take: 2), CancellationToken.None);

        page2.TotalCount.Should().Be(5);
        page2.Results.Select(x => x.Id).Should().Equal("c3", "c4");
    }

    [Fact]
    public async Task Handle_MapsTheCardFields_WithLocalizedValuesAndFallbacks()
    {
        var promotion = Promotion("p1");
        promotion.Name = "SPRING_SYSTEM";
        promotion.Description = "Invariant description";
        promotion.LocalizedDisplayName = new LocalizedString();
        promotion.LocalizedDisplayName.SetValue("de-DE", "Frühlingsaktion");
        promotion.LocalizedLabel = new LocalizedString();
        promotion.LocalizedLabel.SetValue("de-DE", "10% RABATT");

        var handler = Handler(new StubPromotionSearchService([promotion]), new StubCouponSearchService([Coupon("c1", "p1", maxUses: 5, used: 2)]));

        var coupon = (await handler.Handle(Query(cultureName: "de-DE"), CancellationToken.None)).Results.Single();

        coupon.CouponCode.Should().Be("CODE-c1");
        coupon.SystemName.Should().Be("SPRING_SYSTEM");
        coupon.Name.Should().Be("Frühlingsaktion");
        coupon.Label.Should().Be("10% RABATT");
        coupon.Description.Should().Be("Invariant description");
        coupon.MaxUsesNumber.Should().Be(5);
        coupon.TotalUsesCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithoutTheMarketingModule_IsEmpty()
    {
        var handler = new SalesRepCouponsQueryHandler(
            new FakeOrganizationAccessService(isRep: true),
            new StubOptionalDependency<IPromotionSearchService>(null),
            new StubOptionalDependency<ICouponSearchService>(null),
            new StubOptionalDependency<IPromotionUsageSearchService>(null),
            new FakeStoreAccessService(allowed: true));

        var result = await handler.Handle(Query(), CancellationToken.None);

        result.TotalCount.Should().Be(0);
        result.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ForANonRep_IsEmpty_AndSearchesNothing()
    {
        var promotions = new StubPromotionSearchService([Promotion("p1")]);
        var handler = Handler(promotions, new StubCouponSearchService([Coupon("c1", "p1")]), isRep: false);

        var result = await handler.Handle(Query(), CancellationToken.None);

        result.Results.Should().BeEmpty();
        promotions.ReceivedCriteria.Should().BeEmpty();
    }

    // The store comes from the client: another store's coupons must not come back for the asking.
    [Fact]
    public async Task Handle_ForAStoreTheRepIsNotAllowed_IsEmpty_AndSearchesNothing()
    {
        var promotions = new StubPromotionSearchService([Promotion("p1")]);
        var storeAccess = new FakeStoreAccessService(allowed: false);
        var handler = Handler(promotions, new StubCouponSearchService([Coupon("c1", "p1")]), storeAccess: storeAccess);

        var result = await handler.Handle(Query(storeId: "other-store"), CancellationToken.None);

        result.Results.Should().BeEmpty();
        promotions.ReceivedCriteria.Should().BeEmpty();
        storeAccess.Received.Should().Equal(("rep-user", "other-store"));
    }

    // An empty store reads as "every store" to the promotion search, and the access service lets an unnamed store
    // pass, so the handler refuses it itself.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Handle_WithoutAStore_IsEmpty_AndSearchesNothing(string storeId)
    {
        var promotions = new StubPromotionSearchService([Promotion("p1")]);
        var handler = Handler(promotions, new StubCouponSearchService([Coupon("c1", "p1")]));

        var result = await handler.Handle(Query(storeId: storeId), CancellationToken.None);

        result.Results.Should().BeEmpty();
        promotions.ReceivedCriteria.Should().BeEmpty();
    }

    private static SalesRepCouponsQueryHandler Handler(
        StubPromotionSearchService promotions,
        StubCouponSearchService coupons,
        bool isRep = true,
        FakeStoreAccessService storeAccess = null,
        IDictionary<string, int> uses = null) =>
        new(
            new FakeOrganizationAccessService(isRep),
            new StubOptionalDependency<IPromotionSearchService>(promotions),
            new StubOptionalDependency<ICouponSearchService>(coupons),
            // By default the recorded uses agree with the cached coupons.
            new StubOptionalDependency<IPromotionUsageSearchService>(
                new StubPromotionUsageSearchService(uses ?? coupons.Coupons.ToDictionary(x => x.Code, x => (int)x.TotalUsesCount))),
            storeAccess ?? new FakeStoreAccessService(allowed: true));

    private static SalesRepCouponsQuery Query(int skip = 0, int take = 20, string cultureName = null, string storeId = StoreId) => new()
    {
        StoreId = storeId,
        CultureName = cultureName,
        UserId = "rep-user",
        Skip = skip,
        Take = take,
    };

    private static Promotion Promotion(string id, DateTime? endDate = null) => new()
    {
        Id = id,
        Name = $"Promotion {id}",
        EndDate = endDate,
    };

    private static Coupon Coupon(string id, string promotionId, int maxUses = 0, long used = 0, DateTime? expiration = null, string memberId = null) => new()
    {
        Id = id,
        Code = $"CODE-{id}",
        PromotionId = promotionId,
        MaxUsesNumber = maxUses,
        TotalUsesCount = used,
        ExpirationDate = expiration,
        MemberId = memberId,
    };

    private static List<T> Page<T>(IList<T> items, SearchCriteriaBase criteria) =>
        items.Skip(criteria.Skip).Take(criteria.Take).ToList();

    private sealed class StubPromotionSearchService(IList<Promotion> promotions) : IPromotionSearchService
    {
        public List<PromotionSearchCriteria> ReceivedCriteria { get; } = [];

        public Task<PromotionSearchResult> SearchAsync(PromotionSearchCriteria criteria, bool clone = true)
        {
            ReceivedCriteria.Add(criteria);
            return Task.FromResult(new PromotionSearchResult { TotalCount = promotions.Count, Results = Page(promotions, criteria) });
        }
    }

    // Clones on request like the real search, so a handler that writes to cached coupons shows up.
    private sealed class StubCouponSearchService(IList<Coupon> coupons) : ICouponSearchService
    {
        public IList<Coupon> Coupons => coupons;

        public Task<CouponSearchResult> SearchAsync(CouponSearchCriteria criteria, bool clone = true)
        {
            var matches = coupons
                .Where(x => criteria.PromotionIds?.Contains(x.PromotionId) != false)
                .Select(x => clone ? (Coupon)x.Clone() : x)
                .ToList();
            return Task.FromResult(new CouponSearchResult { TotalCount = matches.Count, Results = Page(matches, criteria) });
        }
    }

    private sealed class StubPromotionUsageSearchService(IDictionary<string, int> usesByCode) : IPromotionUsageSearchService
    {
        public Task<PromotionUsageSearchResult> SearchAsync(PromotionUsageSearchCriteria criteria, bool clone = true) =>
            Task.FromResult(new PromotionUsageSearchResult { TotalCount = usesByCode.TryGetValue(criteria.CouponCode, out var uses) ? uses : 0 });
    }

    private sealed class StubOptionalDependency<T>(T value) : IOptionalDependency<T>
        where T : class
    {
        public bool HasValue => value != null;

        public T Value => value;
    }

    // Only the membership lookup is real: IsSalesRepAsync is all the handler asks of it.
    private sealed class FakeOrganizationAccessService(bool isRep) : ISalesRepOrganizationAccessService
    {
        public Task<IList<OrganizationMembership>> GetGrantingMembershipsAsync(IList<string> userIds = null, IList<string> organizationIds = null) =>
            Task.FromResult<IList<OrganizationMembership>>(isRep ? [new OrganizationMembership()] : []);

        public Task<bool> ServesOrganizationAsync(string userId, string organizationId) => throw new NotSupportedException();

        public Task<bool> ServesAllOrganizationsAsync(string userId, IList<string> organizationIds) => throw new NotSupportedException();

        public Task<IList<string>> GetServedOrganizationIdsAsync(string userId) => throw new NotSupportedException();

        public Task<IList<string>> GetVisibleOrganizationIdsAsync(string userId, string organizationId) => throw new NotSupportedException();

        public Task<IList<OrganizationMembership>> GetVisibleGrantingMembershipsAsync(string userId, string organizationId) => throw new NotSupportedException();
    }

    private sealed class FakeStoreAccessService(bool allowed) : ISalesRepStoreAccessService
    {
        public List<(string UserId, string StoreId)> Received { get; } = [];

        public Task<bool> IsAllowedAsync(string userId, string storeId)
        {
            Received.Add((userId, storeId));
            return Task.FromResult(allowed);
        }
    }
}
