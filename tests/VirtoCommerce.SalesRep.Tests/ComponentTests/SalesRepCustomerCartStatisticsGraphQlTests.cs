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
/// End-to-end component tests for the <c>salesRepCustomerCartStatistics</c> X-API query: seed real carts and line
/// items into in-memory SQLite, execute real GraphQL through the real scoped schema / MediatR handler / the real
/// <c>CustomerCartStatisticsService</c>, and assert the aggregated figures. Every figure is aggregated from the LINE
/// ITEMS — the carts only scope them — so the range bounds each line item's modified date, an item-less cart is
/// inert whatever its denormalized <c>LineItemsCount</c> says, and <c>count</c> is the distinct carts holding lines
/// in the range. The cart-level figures (count/total/average) cost an extra COUNT DISTINCT and a currency
/// conversion, so they are aggregated only when the selection asks for one. The default cart-kind service maps the
/// built-in "active-carts" kind to carts named <b>"default"</b> — an include-list on the storefront cart name, since
/// wishlists, saved-for-later and any custom cart kind are Cart rows carrying their own list names.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepCustomerCartStatisticsGraphQlTests
{
    private static readonly DateTime _feb2026 = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _apr2026 = new(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _mar2025 = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string Ytd = "from: \"2026-01-01T00:00:00Z\", to: \"2026-07-11T00:00:00Z\"";

    private const string Wishlist = ModuleConstants.CartType.Wishlist;

    [Fact]
    public async Task Cart_ActiveCarts_SumsTheItemsOfDefaultNamedCarts()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c1", "org-1", 100m, _feb2026);
        SeedCartItem(ctx, "c1", "c1-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026);
        SeedCart(ctx, "c2", "org-1", 200m, _apr2026);
        SeedCartItem(ctx, "c2", "c2-item", quantity: 3, selectedForCheckout: true, modifiedDate: _apr2026);
        SeedCart(ctx, "empty", "org-1", 999m, _feb2026, lineItemsCount: 0);                 // no lines → inert
        SeedCart(ctx, "wish", "org-1", 500m, _feb2026, type: Wishlist, name: "Q1 project"); // list → excluded
        SeedCartItem(ctx, "wish", "wish-item", quantity: 9, selectedForCheckout: true, modifiedDate: _feb2026);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active:   period({{Ytd}}, filter: "active-carts") { selectedItemQuantity unselectedItemQuantity count }
                allCarts: period({{Ytd}}) { selectedItemQuantity count }
              } }
              """,
            userId: rep.UserId);

        var stats = Stats(json);

        var active = stats.GetProperty("active");
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(5); // 2 + 3 (the list's 9 excluded)
        active.GetProperty("unselectedItemQuantity").GetInt32().Should().Be(0);
        active.GetProperty("count").GetInt32().Should().Be(2);                // the two carts holding lines

        // Baseline without a kind filter: every Cart row, the list included.
        var allCarts = stats.GetProperty("allCarts");
        allCarts.GetProperty("selectedItemQuantity").GetInt32().Should().Be(14); // 2 + 3 + 9
        allCarts.GetProperty("count").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Cart_ItemlessCarts_ContributeNothing_WhateverTheDenormalizedCountSays()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "full", "org-1", 100m, _feb2026, lineItemsCount: 3);
        SeedCartItem(ctx, "full", "full-item", quantity: 3, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 10m);
        // Two ways a cart can hold no lines while its denormalized counter disagrees: emptied on checkout (0), and
        // a stale positive counter left by a row written outside the platform. Neither may move any figure, in a
        // dated window just as in an undated one (VCST-5648).
        SeedCart(ctx, "emptied", "org-1", 999m, _feb2026, lineItemsCount: 0);
        SeedCart(ctx, "stale-counter", "org-1", 999m, _feb2026, lineItemsCount: 5);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                dated:    period({{Ytd}}, filter: "active-carts") { selectedItemQuantity count total { amount } }
                lifetime: period(filter: "active-carts") { selectedItemQuantity count total { amount } }
              } }
              """,
            userId: rep.UserId);

        var stats = Stats(json);
        foreach (var window in new[] { "dated", "lifetime" })
        {
            var period = stats.GetProperty(window);
            period.GetProperty("selectedItemQuantity").GetInt32().Should().Be(3, $"the item-less carts add nothing to '{window}'");
            period.GetProperty("count").GetInt32().Should().Be(1, $"only the cart holding lines counts in '{window}'");
            MoneyAmount(period, "total").Should().Be(30m); // the two 999 carts never contribute
        }
    }

    [Fact]
    public async Task Cart_Total_SumsLineSubtotalsAfterLineDiscounts()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c1", "org-1", 9999m, _feb2026); // the cart's own persisted Total must not be read
        SeedCartItem(ctx, "c1", "discounted", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026,
            listPrice: 100m, discountAmount: 10m);                                                  // (100-10)*2 = 180
        SeedCartItem(ctx, "c1", "sub-unit", quantity: 0, selectedForCheckout: true, modifiedDate: _feb2026,
            listPrice: 40m);                                                                        // qty<1 billed as 1 = 40

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { total { amount } count average { amount } } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        MoneyAmount(active, "total").Should().Be(220m);   // 180 + 40, from the LINE prices, not the cart's 9999
        active.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(active, "average").Should().Be(220m); // total / count
    }

    [Fact]
    public async Task Cart_Total_ExcludesUnselectedAndGiftLines_UnlikeTheQuantities()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c1", "org-1", 0m, _feb2026);
        SeedCartItem(ctx, "c1", "selected", quantity: 1, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 50m);
        SeedCartItem(ctx, "c1", "parked", quantity: 4, selectedForCheckout: false, modifiedDate: _feb2026, listPrice: 999m);
        SeedCartItem(ctx, "c1", "freebie", quantity: 3, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 777m, isGift: true);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") {
                  total { amount } selectedItemQuantity unselectedItemQuantity } } }
              """,
            userId: rep.UserId);

        // Money mirrors DefaultShoppingCartTotalsCalculator (selected, non-gift only); the quantities deliberately
        // do not — a parked line is reported separately and a gift still occupies the cart.
        var active = Stats(json).GetProperty("active");
        MoneyAmount(active, "total").Should().Be(50m);
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(4);   // 1 + the 3 gift units
        active.GetProperty("unselectedItemQuantity").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task Cart_Count_CountsOnlyTheCartsContributingToTotal()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "contributing", "org-1", 0m, _feb2026);
        SeedCartItem(ctx, "contributing", "picked", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 30m);
        // Nothing picked for checkout here, and nothing but a gift over there: both hold items, neither adds money.
        SeedCart(ctx, "parked-only", "org-1", 0m, _feb2026);
        SeedCartItem(ctx, "parked-only", "parked", quantity: 5, selectedForCheckout: false, modifiedDate: _feb2026, listPrice: 99m);
        SeedCart(ctx, "gift-only", "org-1", 0m, _feb2026);
        SeedCartItem(ctx, "gift-only", "freebie", quantity: 7, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 99m, isGift: true);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") {
                  count total { amount } average { amount } selectedItemQuantity unselectedItemQuantity } } }
              """,
            userId: rep.UserId);

        // count is the population behind total, so average = total / count exactly; the two non-contributing carts
        // still report their quantities.
        var active = Stats(json).GetProperty("active");
        active.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(active, "total").Should().Be(60m);
        MoneyAmount(active, "average").Should().Be(60m);
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(9);   // 2 + the 7 gift units
        active.GetProperty("unselectedItemQuantity").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task Cart_CartFigures_AreAggregatedOnlyWhenSelected_AndNeverCollideWithAQuantitiesOnlySelection()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c1", "org-1", 0m, _feb2026);
        SeedCartItem(ctx, "c1", "c1-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 25m);

        // Four selections over the SAME range, each resolving to a different response group: quantities only, money
        // only, both, and both behind aliases. They share a DataLoader bucket unless the response group is part of
        // its key — if it is not, whichever ran first answers the rest and the missing family reads 0.
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                itemsOnly: period({{Ytd}}, filter: "active-carts") { selectedItemQuantity }
                moneyOnly: period({{Ytd}}, filter: "active-carts") { count total { amount } }
                withMoney: period({{Ytd}}, filter: "active-carts") { selectedItemQuantity count total { amount } }
                aliased:   period({{Ytd}}, filter: "active-carts") { qty: selectedItemQuantity c: count t: total { amount } }
              } }
              """,
            userId: rep.UserId);

        var stats = Stats(json);
        stats.GetProperty("itemsOnly").GetProperty("selectedItemQuantity").GetInt32().Should().Be(2);

        // Money without any quantity field: the quantities scan is skipped, the money still has to be right.
        var moneyOnly = stats.GetProperty("moneyOnly");
        moneyOnly.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(moneyOnly, "total").Should().Be(50m);

        var withMoney = stats.GetProperty("withMoney");
        withMoney.GetProperty("selectedItemQuantity").GetInt32().Should().Be(2);
        withMoney.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(withMoney, "total").Should().Be(50m);

        // Selection detection reads field NAMES, so an alias must not hide either family from the aggregator.
        var aliased = stats.GetProperty("aliased");
        aliased.GetProperty("qty").GetInt32().Should().Be(2);
        aliased.GetProperty("c").GetInt32().Should().Be(1);
        aliased.GetProperty("t").GetProperty("amount").GetDecimal().Should().Be(50m);
    }

    [Fact]
    public async Task Cart_Comparison_ReportsQuantityAndMoneyDeltas()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c1", "org-1", 0m, _feb2026);
        SeedCartItem(ctx, "c1", "old-line", quantity: 4, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 10m);
        SeedCartItem(ctx, "c1", "fresh-line", quantity: 1, selectedForCheckout: true, modifiedDate: _apr2026, listPrice: 10m);
        SeedCartItem(ctx, "c1", "fresh-parked", quantity: 3, selectedForCheckout: false, modifiedDate: _apr2026, listPrice: 10m);

        // The dashboard's shape: a recent slice against the wider one. Deltas ride on the line-item modified date,
        // so "current" is the April slice (1 selected + 3 parked, $10) against YTD (5 selected, $50).
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                slice: comparison(
                  current:  { from: "2026-04-01T00:00:00Z", to: "2026-07-11T00:00:00Z" }
                  previous: { {{Ytd}} }
                  filter: "active-carts") {
                  selectedItemQuantityChange selectedItemQuantityChangePercent
                  unselectedItemQuantityChange unselectedItemQuantityChangePercent
                  countChange totalChange { amount } totalChangePercent
                }
              } }
              """,
            userId: rep.UserId);

        var slice = Stats(json).GetProperty("slice");
        slice.GetProperty("selectedItemQuantityChange").GetInt32().Should().Be(-4);          // 1 (April) - 5 (YTD)
        slice.GetProperty("selectedItemQuantityChangePercent").GetDecimal().Should().Be(-80m);
        slice.GetProperty("unselectedItemQuantityChange").GetInt32().Should().Be(0);         // 3 in both slices
        slice.GetProperty("unselectedItemQuantityChangePercent").GetDecimal().Should().Be(0m);
        // Both slices see the one cart, so the count is unchanged while the money is not.
        slice.GetProperty("countChange").GetInt32().Should().Be(0);
        MoneyAmount(slice, "totalChange").Should().Be(-40m);                                 // 10 (April) - 50 (YTD)
        slice.GetProperty("totalChangePercent").GetDecimal().Should().Be(-80m);
    }

    [Fact]
    public async Task Cart_Comparison_OfQuantitiesOnly_SharesNoStaleBucketWithAMoneyComparison()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c1", "org-1", 0m, _feb2026);
        SeedCartItem(ctx, "c1", "old-line", quantity: 4, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 10m);
        SeedCartItem(ctx, "c1", "fresh-line", quantity: 1, selectedForCheckout: true, modifiedDate: _apr2026, listPrice: 10m);

        // Same two ranges, three selections: a quantities-only comparison (lean aggregate), a money comparison, and
        // a plain period. Each must read its own figures correctly whatever order the loader batches them in.
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                lean: comparison(
                  current: { from: "2026-04-01T00:00:00Z", to: "2026-07-11T00:00:00Z" }
                  previous: { {{Ytd}} } filter: "active-carts") { selectedItemQuantityChange }
                rich: comparison(
                  current: { from: "2026-04-01T00:00:00Z", to: "2026-07-11T00:00:00Z" }
                  previous: { {{Ytd}} } filter: "active-carts") { selectedItemQuantityChange totalChange { amount } }
                ytd: period({{Ytd}}, filter: "active-carts") { selectedItemQuantity count total { amount } }
              } }
              """,
            userId: rep.UserId);

        var stats = Stats(json);
        stats.GetProperty("lean").GetProperty("selectedItemQuantityChange").GetInt32().Should().Be(-4);

        var rich = stats.GetProperty("rich");
        rich.GetProperty("selectedItemQuantityChange").GetInt32().Should().Be(-4);
        MoneyAmount(rich, "totalChange").Should().Be(-40m);

        var ytd = stats.GetProperty("ytd");
        ytd.GetProperty("selectedItemQuantity").GetInt32().Should().Be(5);
        ytd.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(ytd, "total").Should().Be(50m);
    }

    [Fact]
    public async Task Cart_ExcludesWishlists()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "cart", "org-1", 100m, _feb2026);                     // regular cart → counts
        SeedCartItem(ctx, "cart", "cart-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 10m);
        SeedCart(ctx, "wish", "org-1", 999m, _feb2026,                      // non-empty project → excluded
            type: Wishlist, name: "Christmas list");
        SeedCartItem(ctx, "wish", "wish-item", quantity: 9, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 999m);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { selectedItemQuantity total { amount } } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(2);
        MoneyAmount(active, "total").Should().Be(20m);
    }

    [Fact]
    public async Task Cart_IncludesOnlyDefaultNamedCarts()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "cart", "org-1", 100m, _feb2026);                             // the storefront cart ("default")
        SeedCart(ctx, "later", "org-1", 999m, _feb2026,                              // a saved-for-later list → excluded
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
                active: period({{Ytd}}, filter: "active-carts") { selectedItemQuantity count } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(2); // neither the list's 5 nor the custom 8
        active.GetProperty("count").GetInt32().Should().Be(1);
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
              query { salesRepCustomerCartStatistics(organizationId:"org-1") {
                active: period({{Ytd}}, filter: "active-carts") { selectedItemQuantity unselectedItemQuantity } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
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
              query { salesRepCustomerCartStatistics(organizationId:"org-1") {
                active: period({{Ytd}}, filter: "active-carts") { selectedItemQuantity } } }
              """,
            userId: rep.UserId);

        // A gift line counts like any other towards the quantities (to exclude gifts, filter IsGift in BuildItemQuery).
        Stats(json).GetProperty("active").GetProperty("selectedItemQuantity").GetInt32().Should().Be(8); // 2 (gift) + 6 (stale counter)
    }

    [Fact]
    public async Task Cart_BuildQueryOverride_NarrowsEveryFigure()
    {
        // A subclass narrowing the cart set through the BuildQuery seam must narrow both metric families: they run
        // over that cart set, not over an unfiltered one.
        using var ctx = SalesRepTestContext.Create(services =>
            services.AddTransient<ICustomerCartStatisticsService, MinimumTotalCartStatisticsService>());
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "big", "org-1", 500m, _feb2026);                       // above the override's floor → counts
        SeedCart(ctx, "small", "org-1", 10m, _feb2026);                      // below it → must not contribute
        SeedCartItem(ctx, "big", "big-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 15m);
        SeedCartItem(ctx, "small", "small-item", quantity: 7, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 99m);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { selectedItemQuantity count total { amount } } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(2); // the excluded cart's 7 must not leak
        active.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(active, "total").Should().Be(30m);
    }

    [Fact]
    public async Task Cart_ItemQuantities_ExcludeItemsTouchedBeforeTheRange()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c1", "org-1", 100m, _feb2026); // created inside the range
        SeedCartItem(ctx, "c1", "untouched-line", quantity: 5, selectedForCheckout: true, modifiedDate: _mar2025, listPrice: 20m);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                ytd:      period({{Ytd}}, filter: "active-carts") { selectedItemQuantity count total { amount } }
                lifetime: period(filter: "active-carts") { selectedItemQuantity count total { amount } }
              } }
              """,
            userId: rep.UserId);

        // The mirror image of Cart_ItemQuantities_BoundedByLineItemModifiedDate: the CART falls inside the range but
        // its only line was last touched a year earlier, so the range reports nothing — including no cart — while
        // the lifetime figure sees it.
        var stats = Stats(json);
        var ytd = stats.GetProperty("ytd");
        ytd.GetProperty("selectedItemQuantity").GetInt32().Should().Be(0);
        ytd.GetProperty("count").GetInt32().Should().Be(0);
        MoneyAmount(ytd, "total").Should().Be(0m);

        var lifetime = stats.GetProperty("lifetime");
        lifetime.GetProperty("selectedItemQuantity").GetInt32().Should().Be(5);
        lifetime.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(lifetime, "total").Should().Be(100m);
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
                ytd: period({{Ytd}}, filter: "active-carts") { selectedItemQuantity count } } }
              """,
            userId: rep.UserId);

        // The cart's own dates are never filtered: a cart opened last year still reports the line touched this year,
        // and still counts as a cart with activity in the range.
        var ytd = Stats(json).GetProperty("ytd");
        ytd.GetProperty("selectedItemQuantity").GetInt32().Should().Be(7);
        ytd.GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Cart_CountsOnlyTheRequestedCurrency()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c-usd", "org-1", 0m, _feb2026, currency: "USD");
        SeedCartItem(ctx, "c-usd", "usd-item", quantity: 1, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 100m, currency: "USD");
        SeedCart(ctx, "c-eur", "org-1", 0m, _feb2026, currency: "EUR");
        SeedCartItem(ctx, "c-eur", "eur-item", quantity: 1, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 200m, currency: "EUR");

        // The storefront keeps one cart per currency and mirrors the same contents into each, so the figures
        // follow the requested currency rather than folding every mirror together.
        var usd = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { total { amount } count average { amount } } } }
              """,
            userId: rep.UserId);

        var usdActive = Stats(usd).GetProperty("active");
        MoneyAmount(usdActive, "total").Should().Be(100m); // the EUR cart is out of scope, not converted in
        usdActive.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(usdActive, "average").Should().Be(100m);

        var eur = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "EUR") {
                active: period({{Ytd}}, filter: "active-carts") { total { amount } count } } }
              """,
            userId: rep.UserId);

        var eurActive = Stats(eur).GetProperty("active");
        MoneyAmount(eurActive, "total").Should().Be(200m); // reported in EUR, the requested currency
        eurActive.GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Cart_CurrencyMirrors_DoNotDoubleCount()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        // Exactly what a storefront currency switch leaves behind: the same cart, same name, same contents,
        // stored once per currency (ChangeCartCurrencyCommandHandler copies the lines and keeps both rows).
        SeedCart(ctx, "mirror-usd", "org-1", 0m, _feb2026, currency: "USD");
        SeedCartItem(ctx, "mirror-usd", "usd-line", quantity: 3, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 25m, currency: "USD");
        SeedCart(ctx, "mirror-eur", "org-1", 0m, _feb2026, currency: "EUR");
        SeedCartItem(ctx, "mirror-eur", "eur-line", quantity: 3, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 20m, currency: "EUR");

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { count selectedItemQuantity total { amount } } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(3); // one intent, counted once
        active.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(active, "total").Should().Be(75m); // the USD mirror alone
    }

    [Fact]
    public async Task Cart_ItemQuantities_CountOnlyTheRequestedCurrency()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c-eur", "org-1", 0m, _feb2026, currency: "EUR");
        SeedCartItem(ctx, "c-eur", "eur-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 10m, currency: "EUR");
        SeedCart(ctx, "c-usd", "org-1", 0m, _feb2026, currency: "USD");
        SeedCartItem(ctx, "c-usd", "usd-item", quantity: 3, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 10m, currency: "USD");

        // The quantities follow the cart scoping too: a quantity needs no exchange rate, but the other
        // currency's cart is a mirror of the same intent, so counting it would double the metric.
        var usd = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { count selectedItemQuantity } } }
              """,
            userId: rep.UserId);

        var usdActive = Stats(usd).GetProperty("active");
        usdActive.GetProperty("selectedItemQuantity").GetInt32().Should().Be(3); // not 5 - the EUR cart is another mirror
        usdActive.GetProperty("count").GetInt32().Should().Be(1);

        var eur = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "EUR") {
                active: period({{Ytd}}, filter: "active-carts") { count selectedItemQuantity } } }
              """,
            userId: rep.UserId);

        var eurActive = Stats(eur).GetProperty("active");
        eurActive.GetProperty("selectedItemQuantity").GetInt32().Should().Be(2);
        eurActive.GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Cart_ExcludesCartsInOtherCurrencies_WithoutAWarning()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c-usd", "org-1", 0m, _feb2026, currency: "USD");
        SeedCartItem(ctx, "c-usd", "usd-item", quantity: 3, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 100m, currency: "USD");
        SeedCart(ctx, "c-gbp", "org-1", 0m, _feb2026, currency: "GBP"); // not a configured currency either
        SeedCartItem(ctx, "c-gbp", "gbp-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 999m, currency: "GBP");

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { count total { amount } selectedItemQuantity warning } } }
              """,
            userId: rep.UserId);

        // Another currency is out of scope rather than "excluded from the conversion", so no warning is raised -
        // the currency filter runs before the fold ever sees the row.
        var active = Stats(json).GetProperty("active");
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(3);
        active.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(active, "total").Should().Be(300m);
        active.GetProperty("warning").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Cart_MixedCurrencyCart_CountsOnce()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        // ONE cart, two currencies — the platform models this (a line carries its own currency and the calculator
        // builds a CartTotal per currency), so the per-currency money fold must not turn it into two carts.
        SeedCart(ctx, "mixed", "org-1", 0m, _feb2026, currency: "USD");
        SeedCartItem(ctx, "mixed", "usd-line", quantity: 1, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 100m, currency: "USD");
        SeedCartItem(ctx, "mixed", "eur-line", quantity: 1, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 100m, currency: "EUR");

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") {
                  count total { amount } average { amount } selectedItemQuantity } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        active.GetProperty("count").GetInt32().Should().Be(1);          // one cart, not one per currency
        MoneyAmount(active, "total").Should().Be(225m);                 // 100 USD + 100 EUR * 1.25
        MoneyAmount(active, "average").Should().Be(225m);               // total / 1, not / 2
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Cart_MixedCurrencyCart_WithUnconvertibleLine_StillCountsOnce()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "mixed", "org-1", 0m, _feb2026, currency: "USD");
        SeedCartItem(ctx, "mixed", "usd-line", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 30m, currency: "USD");
        SeedCartItem(ctx, "mixed", "gbp-line", quantity: 1, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 999m, currency: "GBP");

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") {
                  count total { amount } selectedItemQuantity warning } } }
              """,
            userId: rep.UserId);

        // The unconvertible line drops out of the money, but the cart still has a convertible one, so it counts once.
        var active = Stats(json).GetProperty("active");
        active.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(active, "total").Should().Be(60m);
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(3); // 2 (USD) + 1 (GBP)
        active.GetProperty("warning").GetString().Should().Contain("GBP");
    }

    [Fact]
    public async Task Cart_UnconfiguredCurrency_DropsOutOfTheMoneyFiguresOnly()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c-usd", "org-1", 0m, _feb2026, currency: "USD");
        SeedCartItem(ctx, "c-usd", "usd-item", quantity: 3, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 100m, currency: "USD");
        // The foreign line sits inside an IN-SCOPE cart: a whole cart in another currency is filtered out
        // before the fold ever sees it (Cart_ExcludesCartsInOtherCurrencies_WithoutAWarning), so a line in
        // an unconfigured currency is what still reaches the exclusion path.
        SeedCartItem(ctx, "c-usd", "gbp-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 999m, currency: "GBP"); // not a configured currency

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { total { amount } count selectedItemQuantity warning } } }
              """,
            userId: rep.UserId);

        // An unconvertible line drops out of the money figures (and its cart out of count, with a warning), but its
        // items still count — a quantity needs no exchange rate, so excluding them would only understate the metric.
        var active = Stats(json).GetProperty("active");
        MoneyAmount(active, "total").Should().Be(300m);
        active.GetProperty("count").GetInt32().Should().Be(1);
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(5); // 3 (USD) + 2 (GBP)
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
        SeedCartItem(ctx, "live", "live-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 10m);
        SeedCartItem(ctx, "dead", "dead-item", quantity: 7, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 999m);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { selectedItemQuantity count total { amount } } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(2); // a deleted cart's items stay out
        active.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(active, "total").Should().Be(20m);
    }

    [Fact]
    public async Task Cart_KindFilter_UnrecognizedName_FailsClosed()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "c1", "org-1", 100m, _feb2026);
        SeedCartItem(ctx, "c1", "c1-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 10m);

        // An unrecognized kind resolves to an empty filter → zeros, not every cart.
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                bogus: period({{Ytd}}, filter: "DoesNotExist") { selectedItemQuantity unselectedItemQuantity count total { amount } } } }
              """,
            userId: rep.UserId);

        var bogus = Stats(json).GetProperty("bogus");
        bogus.GetProperty("selectedItemQuantity").GetInt32().Should().Be(0);
        bogus.GetProperty("unselectedItemQuantity").GetInt32().Should().Be(0);
        bogus.GetProperty("count").GetInt32().Should().Be(0);
        MoneyAmount(bogus, "total").Should().Be(0m);
    }

    [Fact]
    public async Task Cart_ScopesByStore()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "b2b", "org-1", 100m, _feb2026, storeId: "B2B-store");
        SeedCart(ctx, "other", "org-1", 500m, _feb2026, storeId: "OtherStore");
        SeedCartItem(ctx, "b2b", "b2b-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 10m);
        SeedCartItem(ctx, "other", "other-item", quantity: 7, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 999m);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", storeId: "B2B-store", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { selectedItemQuantity count total { amount } } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(2); // the other store's items stay out
        active.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(active, "total").Should().Be(20m);
    }

    [Fact]
    public async Task Cart_AggregatesAllServedOrgs_WhenOrganizationIdOmitted()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2", "org-3");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");
        SeedCart(ctx, "c1", "org-1", 100m, _feb2026);
        SeedCartItem(ctx, "c1", "c1-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 10m);
        SeedCart(ctx, "c2", "org-2", 200m, _feb2026);
        SeedCartItem(ctx, "c2", "c2-item", quantity: 3, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 10m);
        SeedCart(ctx, "c3", "org-3", 999m, _feb2026); // not served → excluded
        SeedCartItem(ctx, "c3", "c3-item", quantity: 9, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 999m);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") { selectedItemQuantity count total { amount } } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(5); // org-1 + org-2; org-3 excluded
        active.GetProperty("count").GetInt32().Should().Be(2);
        MoneyAmount(active, "total").Should().Be(50m);
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
              query { salesRepCustomerCartStatistics(organizationId:"org-2") { active: period({{Ytd}}, filter: "active-carts") { selectedItemQuantity } } }
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
        SeedCartItem(ctx, "mine", "mine-item", quantity: 2, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 10m);
        SeedCartItem(ctx, "foreign", "foreign-selected", quantity: 7, selectedForCheckout: true, modifiedDate: _feb2026, listPrice: 999m);
        SeedCartItem(ctx, "foreign", "foreign-parked", quantity: 4, selectedForCheckout: false, modifiedDate: _feb2026);

        // Data-isolation invariant: a rep sees only carts they created — a foreign rep's cart in the same served
        // organization must never contribute, to any figure.
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                active: period({{Ytd}}, filter: "active-carts") {
                  selectedItemQuantity unselectedItemQuantity count total { amount } } } }
              """,
            userId: rep.UserId);

        var active = Stats(json).GetProperty("active");
        active.GetProperty("selectedItemQuantity").GetInt32().Should().Be(2);   // not 9 — the foreign 7 must not leak
        active.GetProperty("unselectedItemQuantity").GetInt32().Should().Be(0); // nor the foreign parked 4
        active.GetProperty("count").GetInt32().Should().Be(1);
        MoneyAmount(active, "total").Should().Be(20m);
    }

    [Fact]
    public async Task Cart_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1") { active: period({{Ytd}}, filter: "active-carts") { selectedItemQuantity } } }
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
        => SalesRepTestContext.Node(json, "salesRepCustomerCartStatistics");

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
            // Neither Total nor LineItemsCount is read by the statistics (the line items are) — they are seeded so
            // tests can prove that: a stale LineItemsCount cannot suppress or inflate a figure, and the cart's own
            // Total is only what the BuildQuery-override test filters on.
            Total = total,
            IsDeleted = isDeleted,
            LineItemsCount = lineItemsCount,
            CreatedDate = createdDate,
            ModifiedDate = createdDate,
        });
        db.SaveChanges();
    }

    private static void SeedCartItem(
        SalesRepTestContext ctx, string cartId, string id, int quantity, bool selectedForCheckout, DateTime modifiedDate,
        bool isGift = false, decimal listPrice = 0m, decimal discountAmount = 0m, string currency = "USD")
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
            Currency = currency,
            Quantity = quantity,
            SelectedForCheckout = selectedForCheckout,
            IsGift = isGift,
            ListPrice = listPrice,
            DiscountAmount = discountAmount,
            CreatedDate = modifiedDate,
            ModifiedDate = modifiedDate,
        });
        db.SaveChanges();
    }
}
