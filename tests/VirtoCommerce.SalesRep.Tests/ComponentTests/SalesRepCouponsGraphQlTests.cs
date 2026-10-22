using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.MarketingModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

// salesRepCoupons end to end: the real scoped schema over the real marketing services on in-memory SQLite. What the
// unit tests can't show: which promotions Marketing's own search returns for the criteria, and that a coupon's last use
// hides it although Marketing keeps the coupon cached with its old uses count.
[Trait("Category", "Component")]
public class SalesRepCouponsGraphQlTests
{
    private const string OrgA = "org-a";
    private const string Store = SalesRepTestContext.DefaultStoreId;

    // Marketing's "active" compares now with the promotion's start, so every seeded promotion has one.
    private static readonly DateTime Started = DateTime.UtcNow.AddDays(-10);

    [Fact]
    public async Task ListsTheStoresActivePublicPromotionsWithOneCoupon()
    {
        using var ctx = SalesRepTestContext.Create(withMarketing: true);
        var rep = await SeedRepAsync(ctx, "ann@test.com");

        await SeedPromotionAsync(ctx, "p-listed", [Store], ["LISTED"]);
        await SeedPromotionAsync(ctx, "p-every-store", [], ["EVERYSTORE"]);
        await SeedPromotionAsync(ctx, "p-private", [Store], ["PRIVATE"], isPublic: false);
        await SeedPromotionAsync(ctx, "p-inactive", [Store], ["INACTIVE"], isActive: false);
        await SeedPromotionAsync(ctx, "p-ended", [Store], ["ENDED"], endDate: DateTime.UtcNow.AddDays(-1));
        await SeedPromotionAsync(ctx, "p-batch", [Store], ["BATCHONE", "BATCHTWO"]);
        await SeedPromotionAsync(ctx, "p-other-store", ["other-store"], ["OTHERSTORE"]);

        var coupons = await ListCouponsAsync(ctx, rep);

        // A promotion with no store applies to every store, as in Marketing's own search.
        Codes(coupons).Should().BeEquivalentTo("LISTED", "EVERYSTORE");
        coupons.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task CouponRegisteredToAMember_IsNotListed()
    {
        using var ctx = SalesRepTestContext.Create(withMarketing: true);
        var rep = await SeedRepAsync(ctx, "ann@test.com");
        await SeedPromotionAsync(ctx, "p-personal", [Store], ["PERSONAL"], memberId: "org-z");
        await SeedPromotionAsync(ctx, "p-open", [Store], ["OPEN"]);

        var json = await QueryCouponsAsync(ctx, rep);

        Codes(SalesRepTestContext.Node(json, "salesRepCoupons")).Should().Equal("OPEN");
        json.Should().NotContain("PERSONAL").And.NotContain("org-z");
    }

    [Fact]
    public async Task UsedUpCoupon_IsHiddenAfterItsLastUse_ThoughMarketingStillCachesTheCoupon()
    {
        using var ctx = SalesRepTestContext.Create(withMarketing: true);
        var rep = await SeedRepAsync(ctx, "ann@test.com");
        await SeedPromotionAsync(ctx, "p-once", [Store], ["ONCE"], maxUses: 1);
        await SeedPromotionAsync(ctx, "p-thrice", [Store], ["THRICE"], maxUses: 3);

        // The first read loads both coupons into Marketing's cache, each with 0 uses.
        Codes(await ListCouponsAsync(ctx, rep)).Should().BeEquivalentTo("ONCE", "THRICE");

        // Recorded the way checkout records a use: through the usage service, which leaves the cached coupons as they are.
        await RecordUseAsync(ctx, "p-once", "ONCE");
        await RecordUseAsync(ctx, "p-thrice", "THRICE");

        var after = await ListCouponsAsync(ctx, rep);
        Codes(after).Should().Equal("THRICE");
        after.GetProperty("items")[0].GetProperty("totalUsesCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task WithoutTheMarketingModule_AnswersEmpty()
    {
        using var ctx = SalesRepTestContext.Create();
        var rep = await SeedRepAsync(ctx, "ann@test.com");

        // Node(...) also asserts there are no errors: the field answers rather than disappears.
        var coupons = await ListCouponsAsync(ctx, rep);

        coupons.GetProperty("totalCount").GetInt32().Should().Be(0);
        coupons.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Anonymous_IsDenied_AndANonRepSeesNothing()
    {
        using var ctx = SalesRepTestContext.Create(withMarketing: true);
        await SeedPromotionAsync(ctx, "p-listed", [Store], ["LISTED"]);
        var query = $"query {{ salesRepCoupons(storeId: \"{Store}\") {{ totalCount items {{ couponCode }} }} }}";

        var anonymous = await ctx.ExecuteGraphQlAnonymousAsync(query);
        anonymous.Should().Contain("\"errors\"").And.MatchRegex("(?i)anonym").And.NotContain("LISTED");

        var contact = await ctx.SeedContactAsync("outsider-contact");
        var outsiderUserId = await ctx.CreateAccountWithoutRolesAsync(contact.Id, "outsider@test.com");
        var json = await ctx.ExecuteGraphQlAsync(query, userId: outsiderUserId, memberId: contact.Id);

        SalesRepTestContext.Node(json, "salesRepCoupons").GetProperty("totalCount").GetInt32().Should().Be(0);
        json.Should().NotContain("LISTED");
    }

    [Fact]
    public async Task ARepOfAnotherStore_SeesNothing_UntilTheStoreTrustsTheirs()
    {
        using var ctx = SalesRepTestContext.Create(withMarketing: true);
        var rep = await SeedRepAsync(ctx, "bob@test.com", storeId: "other-store");
        await SeedPromotionAsync(ctx, "p-listed", [Store], ["LISTED"]);

        (await ListCouponsAsync(ctx, rep)).GetProperty("totalCount").GetInt32().Should().Be(0);

        ctx.SetStoreTrustedGroups(Store, "other-store");

        Codes(await ListCouponsAsync(ctx, rep)).Should().Equal("LISTED");
    }

    [Fact]
    public async Task Paging_TakesTheOffsetAsTheCursor_OverTheSoonestToExpireOrder()
    {
        using var ctx = SalesRepTestContext.Create(withMarketing: true);
        var rep = await SeedRepAsync(ctx, "ann@test.com");
        await SeedPromotionAsync(ctx, "p-third", [Store], ["THIRD"], endDate: DateTime.UtcNow.AddDays(15));
        await SeedPromotionAsync(ctx, "p-first", [Store], ["FIRST"], endDate: DateTime.UtcNow.AddDays(5));
        await SeedPromotionAsync(ctx, "p-second", [Store], ["SECOND"], endDate: DateTime.UtcNow.AddDays(10));

        var page1 = await ListCouponsAsync(ctx, rep, ", first: 2");
        page1.GetProperty("totalCount").GetInt32().Should().Be(3);
        Codes(page1).Should().Equal("FIRST", "SECOND");

        Codes(await ListCouponsAsync(ctx, rep, ", first: 2, after: \"2\"")).Should().Equal("THIRD");
    }

    private static async Task<SalesRepDetails> SeedRepAsync(SalesRepTestContext ctx, string email, string storeId = Store)
    {
        await ctx.SeedOrganizationsAsync(OrgA);
        return await ctx.CreateRepInStoreAsync("Ann", "Rep", email, storeId, OrgA);
    }

    private static async Task SeedPromotionAsync(
        SalesRepTestContext ctx,
        string id,
        string[] stores,
        string[] codes,
        bool isPublic = true,
        bool isActive = true,
        DateTime? endDate = null,
        int maxUses = 0,
        string memberId = null)
    {
        var promotion = AbstractTypeFactory<Promotion>.TryCreateInstance();
        promotion.Id = id;
        promotion.Name = id;
        promotion.IsPublic = isPublic;
        promotion.IsActive = isActive;
        promotion.StartDate = Started;
        promotion.EndDate = endDate ?? DateTime.UtcNow.AddDays(30);
        promotion.StoreIds = stores;
        await ctx.GetRequiredService<IPromotionService>().SaveChangesAsync([promotion]);

        var coupons = codes
            .Select(code =>
            {
                var coupon = AbstractTypeFactory<Coupon>.TryCreateInstance();
                coupon.Code = code;
                coupon.PromotionId = id;
                coupon.MaxUsesNumber = maxUses;
                coupon.MemberId = memberId;
                return coupon;
            })
            .ToList();
        await ctx.GetRequiredService<ICouponService>().SaveChangesAsync(coupons);
    }

    private static Task RecordUseAsync(SalesRepTestContext ctx, string promotionId, string couponCode)
    {
        var usage = AbstractTypeFactory<PromotionUsage>.TryCreateInstance();
        usage.PromotionId = promotionId;
        usage.CouponCode = couponCode;
        usage.ObjectId = $"order-{Guid.NewGuid():N}";
        usage.ObjectType = "CustomerOrder";
        usage.UserId = "buyer-1";

        return ctx.GetRequiredService<IPromotionUsageService>().SaveChangesAsync([usage]);
    }

    private static Task<string> QueryCouponsAsync(SalesRepTestContext ctx, SalesRepDetails rep, string arguments = "") =>
        ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepCoupons(storeId: \"{Store}\"{arguments}) {{ totalCount items {{ couponCode maxUsesNumber totalUsesCount endDate }} }} }}",
            userId: rep.UserId,
            memberId: rep.Id);

    private static async Task<JsonElement> ListCouponsAsync(SalesRepTestContext ctx, SalesRepDetails rep, string arguments = "") =>
        SalesRepTestContext.Node(await QueryCouponsAsync(ctx, rep, arguments), "salesRepCoupons");

    private static string[] Codes(JsonElement coupons) =>
        coupons.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("couponCode").GetString()).ToArray();
}
