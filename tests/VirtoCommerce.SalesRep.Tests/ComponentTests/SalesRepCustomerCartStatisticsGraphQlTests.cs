using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CartModule.Core;
using VirtoCommerce.CartModule.Data.Model;
using VirtoCommerce.SalesRep.Core.Services.Statistics;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// End-to-end component tests for the <c>salesRepCustomerCartStatistics</c> X-API query: seed real carts into
/// in-memory SQLite, execute real GraphQL through the real scoped schema / MediatR handler / the real
/// <c>CustomerCartStatisticsService</c>, and assert the aggregated, currency-converted numbers. Money fields are
/// MoneyType, so numeric values are asserted via <c>{ amount }</c>. The default cart-kind service maps the built-in
/// "active-carts" kind to <b>non-empty</b> (LineItemsCount &gt; 0) carts named <b>"default"</b> — an include-list on
/// the storefront cart name, since wishlists, saved-for-later and any custom cart kind are Cart rows carrying their
/// own list names.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepCustomerCartStatisticsGraphQlTests
{
    private static readonly DateTime _feb2026 = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _apr2026 = new(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _mar2025 = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string Ytd = "from: \"2026-01-01T00:00:00Z\", to: \"2026-07-11T00:00:00Z\"";
    private const string LastYear = "from: \"2025-01-01T00:00:00Z\", to: \"2026-01-01T00:00:00Z\"";

    private const string Wishlist = ModuleConstants.CartType.Wishlist;

    [Fact]
    public async Task Cart_ActiveCarts_CountsDefaultNamedNonEmpty_WithComparison()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c1", "org-1", 100m, _feb2026);                          // active cart
        SeedCart(ctx, "c2", "org-1", 200m, _apr2026);                          // active cart
        SeedCart(ctx, "cprev", "org-1", 40m, _mar2025);                        // active, previous year
        SeedCart(ctx, "empty", "org-1", 999m, _feb2026, lineItemsCount: 0);    // empty → excluded
        SeedCart(ctx, "wish", "org-1", 500m, _feb2026, type: Wishlist, name: "Q1 project"); // list → excluded

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                currencyCode
                active:   period({{Ytd}}, filter: "active-carts") { total { amount } count average { amount } lastCartDate }
                allCarts: period({{Ytd}}) { count }
                yoy: comparison(current: { {{Ytd}} }, previous: { {{LastYear}} }, filter: "active-carts") {
                  countChange countChangePercent totalChange { amount }
                }
              } }
              """,
            userId: rep.UserId);

        var stats = Stats(json);
        stats.GetProperty("currencyCode").GetString().Should().Be("USD");

        var active = stats.GetProperty("active");
        MoneyAmount(active, "total").Should().Be(300m); // 100 + 200 (empty + wishlist excluded)
        active.GetProperty("count").GetInt32().Should().Be(2);
        MoneyAmount(active, "average").Should().Be(150m);
        active.GetProperty("lastCartDate").GetDateTime().Should().Be(_apr2026);

        stats.GetProperty("allCarts").GetProperty("count").GetInt32().Should().Be(4); // baseline: c1, c2, empty, wishlist

        var yoy = stats.GetProperty("yoy");
        yoy.GetProperty("countChange").GetInt32().Should().Be(1);            // 2 (YTD) - 1 (prev year)
        yoy.GetProperty("countChangePercent").GetDecimal().Should().Be(100m);
        MoneyAmount(yoy, "totalChange").Should().Be(260m);                    // 300 - 40
    }

    [Fact]
    public async Task Cart_ExcludesEmptyCarts()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "full", "org-1", 100m, _feb2026, lineItemsCount: 3);
        SeedCart(ctx, "emptied", "org-1", 999m, _feb2026, lineItemsCount: 0); // e.g. its order was placed → LineItemsCount 0

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { total { amount } count } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        MoneyAmount(active, "total").Should().Be(100m); // the emptied 999 never counts
        active.GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Cart_ExcludesWishlists()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "cart", "org-1", 100m, _feb2026);                     // regular non-empty cart → counts
        SeedCart(ctx, "wish", "org-1", 999m, _feb2026,                      // non-empty project → excluded
            type: Wishlist, name: "Christmas list");

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { total { amount } count } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        MoneyAmount(active, "total").Should().Be(100m); // the wishlist (project) is not an active cart
        active.GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Cart_IncludesOnlyDefaultNamedCarts()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "cart", "org-1", 100m, _feb2026);                             // the storefront cart ("default")
        SeedCart(ctx, "later", "org-1", 999m, _feb2026,                             // a saved-for-later list → excluded
            type: ModuleConstants.CartType.SavedForLater, name: "Saved for later");
        // A cart kind the module knows nothing about (what a custom project would add): excluded by the name
        // include-list, with no code change — an exclude-list of known types would have let it into the metrics.
        SeedCart(ctx, "custom", "org-1", 777m, _feb2026, type: "CustomProjectKind", name: "Q3 negotiation");
        SeedCartItem(ctx, "cart", "cart-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026);
        SeedCartItem(ctx, "later", "later-item", quantity: 5, selectedForCheckout: true, modifiedDate: _feb2026);
        SeedCartItem(ctx, "custom", "custom-item", quantity: 8, selectedForCheckout: true, modifiedDate: _feb2026);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { total { amount } count selectedItemQuantity } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        MoneyAmount(active, "total").Should().Be(100m);
        active.GetProperty("count").GetInt32().Should().Be(1);
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(2); // neither the list's 5 nor the custom 8
    }

    [Fact]
    public async Task Cart_ItemQuantities_SplitBySelectedForCheckout()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c1", "org-1", 100m, _feb2026);
        SeedCart(ctx, "c2", "org-1", 200m, _apr2026);
        SeedCartItem(ctx, "c1", "c1-selected", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026);
        SeedCartItem(ctx, "c1", "c1-parked", quantity: 4, selectedForCheckout: false, modifiedDate: _feb2026);
        SeedCartItem(ctx, "c2", "c2-selected", quantity: 3, selectedForCheckout: true, modifiedDate: _apr2026);
        SeedCart(ctx, "wish", "org-1", 500m, _feb2026, type: Wishlist, name: "Wish list");
        SeedCartItem(ctx, "wish", "wish-item", quantity: 9, selectedForCheckout: true, modifiedDate: _feb2026);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { count selectedItemQuantity unselectedItemQuantity } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        active.GetProperty("count").GetInt32().Should().Be(2);
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(5);   // 2 + 3, across both carts
        active.GetProperty("unselectedItemQuantity").GetInt32().Should().Be(4); // the parked line only
    }

    [Fact]
    public async Task Cart_ItemQuantities_ReadTheLineItems_NotTheDenormalizedCount()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        // Cart.LineItemsCount is denormalized AND counts non-gift lines only, so it reads 0 in two different
        // situations: legitimately for a promo-gift-only cart, and staler-than-reality for a row written outside
        // the platform (SQL seed / import). Quantities must come from the line items in both.
        SeedCart(ctx, "gift-only", "org-1", 0m, _feb2026, lineItemsCount: 0);
        SeedCartItem(ctx, "gift-only", "gift-line", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026, isGift: true);
        SeedCart(ctx, "stale", "org-1", 100m, _feb2026, lineItemsCount: 0);
        SeedCartItem(ctx, "stale", "stale-line", quantity: 6, selectedForCheckout: true, modifiedDate: _feb2026);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { count selectedItemQuantity } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        active.GetProperty("count").GetInt32().Should().Be(0);                // cart-level figures keep the guard
        // Item figures do not — and a gift line counts like any other (to exclude gifts, filter IsGift in BuildItemQuery).
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(8); // 2 (gift) + 6 (stale counter)
    }

    [Fact]
    public async Task Cart_BuildQueryOverride_NarrowsItemQuantitiesToo()
    {
        // A subclass narrowing the cart set through the BuildQuery seam must narrow BOTH metric families: the item
        // quantities run over the same cart set (dates cleared), not over an unfiltered one.
        using var ctx = SalesRepTestContext.Create(services =>
            services.AddTransient<ICustomerCartStatisticsService, MinimumTotalCartStatisticsService>());
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "big", "org-1", 500m, _feb2026);                       // above the override's floor → counts
        SeedCart(ctx, "small", "org-1", 10m, _feb2026);                      // below it → must not contribute
        SeedCartItem(ctx, "big", "big-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026);
        SeedCartItem(ctx, "small", "small-item", quantity: 7, selectedForCheckout: true, modifiedDate: _feb2026);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { count selectedItemQuantity } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        active.GetProperty("count").GetInt32().Should().Be(1);
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(2); // the excluded cart's 7 must not leak
    }

    [Fact]
    public async Task Cart_ItemQuantities_Comparison_ReportsDeltas()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c1", "org-1", 100m, _feb2026);
        SeedCartItem(ctx, "c1", "old-line", quantity: 4, selectedForCheckout: true, modifiedDate: _feb2026);
        SeedCartItem(ctx, "c1", "fresh-line", quantity: 1, selectedForCheckout: true, modifiedDate: _apr2026);
        SeedCartItem(ctx, "c1", "fresh-parked", quantity: 3, selectedForCheckout: false, modifiedDate: _apr2026);

        // The dashboard's shape: a recent slice against the wider one. Item deltas ride on the line-item
        // modified date, so "current" here is the April slice (1 selected + 3 parked) against YTD (5 selected).
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                slice: comparison(
                  current:  { from: "2026-04-01T00:00:00Z", to: "2026-07-11T00:00:00Z" }
                  previous: { {{Ytd}} }
                  filter: "active-carts") {
                  selectedItemQuantityChange selectedItemQuantityChangePercent
                  unselectedItemQuantityChange unselectedItemQuantityChangePercent
                }
              } }
              """,
            userId: rep.UserId);

        var slice = Stats(json).GetProperty("slice");
        slice.GetProperty("selectedItemQuantityChange").GetInt32().Should().Be(-4);          // 1 (April) - 5 (YTD)
        slice.GetProperty("selectedItemQuantityChangePercent").GetDecimal().Should().Be(-80m);
        slice.GetProperty("unselectedItemQuantityChange").GetInt32().Should().Be(0);         // 3 in both slices
        // Percent is null-safe rather than a divide-by-zero: the previous parked quantity is non-zero here.
        slice.GetProperty("unselectedItemQuantityChangePercent").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task Cart_ItemQuantities_ExcludeItemsTouchedBeforeTheRange()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c1", "org-1", 100m, _feb2026); // created inside the range
        SeedCartItem(ctx, "c1", "untouched-line", quantity: 5, selectedForCheckout: true, modifiedDate: _mar2025);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                ytd: period({{Ytd}}, filter: "active-carts") { count selectedItemQuantity } } }
              """,
            userId: rep.UserId);

        // The mirror image of Cart_ItemQuantities_BoundedByLineItemModifiedDate: here the CART falls inside the
        // range but its only line was last touched a year earlier, so the cart counts and its items do not.
        var ytd = Stats(json).GetProperty("ytd");
        ytd.GetProperty("count").GetInt32().Should().Be(1);
        ytd.GetProperty("selectedItemQuantity").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Cart_ItemQuantities_BoundedByLineItemModifiedDate()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "old", "org-1", 100m, _mar2025); // cart opened last year
        SeedCartItem(ctx, "old", "touched-now", quantity: 7, selectedForCheckout: true, modifiedDate: _apr2026);
        SeedCartItem(ctx, "old", "untouched", quantity: 9, selectedForCheckout: true, modifiedDate: _mar2025);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                ytd: period({{Ytd}}, filter: "active-carts") { count selectedItemQuantity } } }
              """,
            userId: rep.UserId);

        // Cart-level figures follow the CART's created date (last year → out of the window); item quantities follow
        // each LINE ITEM's modified date, so only the line touched this year contributes.
        var ytd = Stats(json).GetProperty("ytd");
        ytd.GetProperty("count").GetInt32().Should().Be(0);
        ytd.GetProperty("selectedItemQuantity").GetInt32().Should().Be(7);
    }

    [Fact]
    public async Task Cart_FoldsMultipleCurrencies_IntoRequestedUsd()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c-usd", "org-1", 100m, _feb2026, currency: "USD");
        SeedCart(ctx, "c-eur", "org-1", 100m, _feb2026, currency: "EUR");

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { total { amount } count average { amount } } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        MoneyAmount(active, "total").Should().Be(225m); // 100 USD + 100 EUR * 1.25
        active.GetProperty("count").GetInt32().Should().Be(2);
        MoneyAmount(active, "average").Should().Be(112.5m);
    }

    [Fact]
    public async Task Cart_ItemQuantities_SumAcrossCurrencies()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c-eur", "org-1", 100m, _feb2026, currency: "EUR");
        SeedCartItem(ctx, "c-eur", "eur-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026);
        SeedCart(ctx, "c-usd", "org-1", 100m, _feb2026, currency: "USD");
        SeedCartItem(ctx, "c-usd", "usd-item", quantity: 3, selectedForCheckout: true, modifiedDate: _feb2026);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { count total { amount } selectedItemQuantity } } }
              """,
            userId: rep.UserId);

        // Quantities are unit counts: summed straight across currencies, no conversion — while the money figures
        // are folded into the requested currency.
        var active = Stats(json).GetProperty("active");
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(5); // 2 (EUR) + 3 (USD)
        active.GetProperty("count").GetInt32().Should().Be(2);
        MoneyAmount(active, "total").Should().Be(225m);                       // 100 USD + 100 EUR * 1.25
    }

    [Fact]
    public async Task Cart_ItemQuantities_IncludeUnconfiguredCurrencyCarts_UnlikeMoneyFigures()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c-usd", "org-1", 100m, _feb2026, currency: "USD");
        SeedCartItem(ctx, "c-usd", "usd-item", quantity: 3, selectedForCheckout: true, modifiedDate: _feb2026);
        SeedCart(ctx, "c-gbp", "org-1", 999m, _feb2026, currency: "GBP"); // not a configured currency
        SeedCartItem(ctx, "c-gbp", "gbp-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { count total { amount } selectedItemQuantity warning } } }
              """,
            userId: rep.UserId);

        // An unconvertible cart drops out of the money figures (and out of count, with a warning), but its items
        // still count — a quantity needs no exchange rate, so excluding them would only understate the metric.
        var active = Stats(json).GetProperty("active");
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(5); // 3 (USD) + 2 (GBP)
        active.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(active, "total").Should().Be(100m);
        active.GetProperty("warning").GetString().Should().Contain("GBP");
    }

    [Fact]
    public async Task Cart_ExcludesDeletedCarts()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "live", "org-1", 100m, _feb2026);
        SeedCart(ctx, "dead", "org-1", 999m, _feb2026, isDeleted: true);
        SeedCartItem(ctx, "live", "live-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026);
        SeedCartItem(ctx, "dead", "dead-item", quantity: 7, selectedForCheckout: true, modifiedDate: _feb2026);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { total { amount } count selectedItemQuantity } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        MoneyAmount(active, "total").Should().Be(100m); // the deleted 999 never counts
        active.GetProperty("count").GetInt32().Should().Be(1);
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(2); // nor do a deleted cart's items
    }

    [Fact]
    public async Task Cart_KindFilter_UnrecognizedName_FailsClosed()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c1", "org-1", 100m, _feb2026);

        // An unrecognized kind resolves to an empty filter → zeros, not every cart.
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                bogus: period({{Ytd}}, filter: "DoesNotExist") { total { amount } count } } }
              """,
            userId: rep.UserId);

        var bogus = Stats(json).GetProperty("bogus");
        MoneyAmount(bogus, "total").Should().Be(0m);
        bogus.GetProperty("count").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Cart_ScopesByStore()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "b2b", "org-1", 100m, _feb2026, storeId: "B2B-store");
        SeedCart(ctx, "other", "org-1", 500m, _feb2026, storeId: "OtherStore");
        SeedCartItem(ctx, "b2b", "b2b-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026);
        SeedCartItem(ctx, "other", "other-item", quantity: 7, selectedForCheckout: true, modifiedDate: _feb2026);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", storeId: "B2B-store", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { total { amount } count selectedItemQuantity } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        MoneyAmount(active, "total").Should().Be(100m);
        active.GetProperty("count").GetInt32().Should().Be(1);
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(2); // the other store's items stay out
    }

    [Fact]
    public async Task Cart_AggregatesAllServedOrgs_WhenOrganizationIdOmitted()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2", "org-3");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");
        SeedCart(ctx, "c1", "org-1", 100m, _feb2026);
        SeedCart(ctx, "c2", "org-2", 200m, _feb2026);
        SeedCart(ctx, "c3", "org-3", 999m, _feb2026); // not served → excluded

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { total { amount } count } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        MoneyAmount(active, "total").Should().Be(300m); // org-1 + org-2; org-3 excluded
        active.GetProperty("count").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Cart_ForOrganizationNotServed_ReturnsNull()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "leak", "org-2", 100m, _feb2026);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-2", currencyCode: "USD") { active: period({{Ytd}}, filter: "active-carts") { count } } }
              """,
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomerCartStatistics\":null");
    }

    [Fact]
    public async Task Cart_ExcludesCartsNotCreatedByRep()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "mine", "org-1", 100m, _feb2026);                                       // created by the rep
        SeedCart(ctx, "foreign", "org-1", 999m, _feb2026, createdByUserId: "another-rep-user"); // same served org, ANOTHER rep
        SeedCartItem(ctx, "mine", "mine-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026);
        SeedCartItem(ctx, "foreign", "foreign-selected", quantity: 7, selectedForCheckout: true, modifiedDate: _feb2026);
        SeedCartItem(ctx, "foreign", "foreign-parked", quantity: 4, selectedForCheckout: false, modifiedDate: _feb2026);

        // Data-isolation invariant: a rep sees only carts they created — a foreign rep's cart in the same served
        // organization must never contribute, to the item quantities just as much as to the money figures.
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") {
                  total { amount } count selectedItemQuantity unselectedItemQuantity } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        MoneyAmount(active, "total").Should().Be(100m); // only the rep's own cart; the foreign 999 excluded
        active.GetProperty("count").GetInt32().Should().Be(1);
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(2);   // not 9 — the foreign 7 must not leak
        active.GetProperty("unselectedItemQuantity").GetInt32().Should().Be(0); // nor the foreign parked 4
    }

    [Fact]
    public async Task Cart_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") { active: period({{Ytd}}, filter: "active-carts") { count } } }
              """);

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    [Fact]
    public async Task Cart_FilterRules_ExposesActiveCartsKind()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // Discovery query the storefront reads to build the cart-kind filter UI. The default resolver exposes a single
        // built-in "active-carts" kind (send its name back as the salesRepCustomerCartStatistics filter argument).
        // Rule discovery is sales-rep-only, so the caller must hold a granting membership.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCartFilterRules(storeId:\"B2B-store\") { name localizedName } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"name\":\"active-carts\"").And.Contain("Active carts");
    }

    [Fact]
    public async Task Cart_FilterRules_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            "query { salesRepCartFilterRules(storeId:\"B2B-store\") { name } }");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    // ---- helpers ----

    private static JsonElement Stats(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("errors", out _).Should().BeFalse("GraphQL response should carry no errors: {0}", json);
        return root.GetProperty("data").GetProperty("salesRepCustomerCartStatistics").Clone();
    }

    private static decimal MoneyAmount(JsonElement parent, string field)
        => parent.GetProperty(field).GetProperty("amount").GetDecimal();

    private static void SeedCart(
        SalesRepTestContext ctx, string id, string org, decimal total, DateTime createdDate,
        string type = null, string status = null, string currency = "USD", string storeId = "B2B-store",
        bool isDeleted = false, string createdByUserId = null, int lineItemsCount = 1,
        string name = ModuleConstants.DefaultCartName)
    {
        using var db = ctx.NewCartDbContext();
        db.Add(new ShoppingCartEntity
        {
            Id = id,
            Name = name,
            CheckoutId = id, // [Required] on ShoppingCartEntity
            OrganizationId = org,
            // A rep builds a cart for the customer, so the cart's CustomerId is the rep's user id; default the
            // creator to the test's rep. Pass createdByUserId to simulate another rep's cart.
            CustomerId = createdByUserId ?? ctx.LastCreatedRepUserId ?? "customer-1",
            CustomerName = "Customer 1",
            StoreId = storeId,
            Type = type,
            Status = status,
            Currency = currency,
            Total = total,
            IsDeleted = isDeleted,
            // The persisted line-item count is the "active" signal (no line-item rows needed to test the metric).
            LineItemsCount = lineItemsCount,
            CreatedDate = createdDate,
            ModifiedDate = createdDate,
        });
        db.SaveChanges();
    }

    private static void SeedCartItem(
        SalesRepTestContext ctx, string cartId, string id, int quantity, bool selectedForCheckout, DateTime modifiedDate,
        bool isGift = false)
    {
        using var db = ctx.NewCartDbContext();
        db.Add(new LineItemEntity
        {
            Id = id,
            ShoppingCartId = cartId,
            ProductId = id,
            CatalogId = "catalog-1",
            Sku = id,
            Name = id,
            Currency = "USD",
            Quantity = quantity,
            SelectedForCheckout = selectedForCheckout,
            IsGift = isGift,
            CreatedDate = modifiedDate,
            ModifiedDate = modifiedDate,
        });
        db.SaveChanges();
    }
}
