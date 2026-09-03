using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CartModule.Core.Events;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Events;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;
using CartLineItemEntity = VirtoCommerce.CartModule.Data.Model.LineItemEntity;
using CartModuleConstants = VirtoCommerce.CartModule.Core.ModuleConstants;
using OrderLineItem = VirtoCommerce.OrdersModule.Core.Model.LineItem;
using OrderLineItemEntity = VirtoCommerce.OrdersModule.Data.Model.LineItemEntity;
using ShoppingCartEntity = VirtoCommerce.CartModule.Data.Model.ShoppingCartEntity;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// Component tests for the statistics cache's invalidation (VCST-5755). The aggregates are read through the real
/// GraphQL path over the real <see cref="VirtoCommerce.Platform.Core.Caching.IPlatformMemoryCache"/>, so every test
/// carries its own control: the same query, same arguments, re-issued after a database change that no event
/// announced, must still answer from the cache. Without that half, a passing "it went fresh" assertion would also
/// pass with no cache at all.
/// <para>
/// Organization ids are unique per test: the platform's cache-region token dictionaries are static, so tests sharing
/// an organization id would expire each other's entries.
/// </para>
/// </summary>
[Trait("Category", "Component")]
public class SalesRepStatisticsCacheInvalidationTests
{
    private static readonly DateTime _feb2026 = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string CartQuery =
        """
        query { salesRepCustomerCartStatistics(organizationId:"{org}", currencyCode: "USD") {
          lifetime: period(filter: "active-carts") { selectedItemQuantity count }
        } }
        """;

    private const string OrderQuery =
        """
        query { salesRepCustomerOrderStatistics(organizationId:"{org}", currencyCode: "USD") {
          lifetime: period { total { amount } count }
        } }
        """;

    [Fact]
    public async Task CartStatistics_AreServedFromTheCacheUntilTheCartChangeArrives()
    {
        const string org = "invalidation-cart-org";
        using var ctx = SalesRepTestContext.Create();
        var rep = await CreateRepAsync(ctx, org);
        SeedCartWithItem(ctx, "c1", org, quantity: 2);

        (await ReadCartQuantityAsync(ctx, org, rep)).Should().Be(2);

        // A change nothing announced: the cached answer must survive it, or the test below proves nothing.
        SeedCartWithItem(ctx, "c2", org, quantity: 3);
        (await ReadCartQuantityAsync(ctx, org, rep)).Should().Be(2);

        await PublishCartChangedAsync(ctx, org);

        (await ReadCartQuantityAsync(ctx, org, rep)).Should().Be(5);
    }

    [Fact]
    public async Task CartStatistics_SurviveAChangeInAnotherOrganization()
    {
        const string org = "invalidation-cart-scope-org";
        const string otherOrg = "invalidation-cart-scope-other-org";
        using var ctx = SalesRepTestContext.Create();
        var rep = await CreateRepAsync(ctx, org, otherOrg);
        SeedCartWithItem(ctx, "c1", org, quantity: 2);

        (await ReadCartQuantityAsync(ctx, org, rep)).Should().Be(2);

        SeedCartWithItem(ctx, "c2", org, quantity: 3);
        await PublishCartChangedAsync(ctx, otherOrg);
        (await ReadCartQuantityAsync(ctx, org, rep)).Should().Be(2);

        await PublishCartChangedAsync(ctx, org);

        (await ReadCartQuantityAsync(ctx, org, rep)).Should().Be(5);
    }

    [Fact]
    public async Task CartStatistics_SurviveACartChangeWhenTheFamilyIsTtlOnly()
    {
        const string org = "invalidation-cart-flag-org";
        using var ctx = SalesRepTestContext.Create();
        ctx.SetSetting(ModuleConstants.Settings.Caching.CartStatisticsInvalidateOnChange, false);
        var rep = await CreateRepAsync(ctx, org);
        SeedCartWithItem(ctx, "c1", org, quantity: 2);

        (await ReadCartQuantityAsync(ctx, org, rep)).Should().Be(2);

        SeedCartWithItem(ctx, "c2", org, quantity: 3);
        await PublishCartChangedAsync(ctx, org);

        // The flag is consulted when the entry is created as well, so no token was ever attached to expire.
        (await ReadCartQuantityAsync(ctx, org, rep)).Should().Be(2);
    }

    [Fact]
    public async Task CartStatistics_AreRecomputedEveryReadWhenCachingIsDisabled()
    {
        const string org = "invalidation-cart-nocache-org";
        using var ctx = SalesRepTestContext.Create();
        ctx.SetSetting(ModuleConstants.Settings.Caching.CartStatisticsCacheExpiration, 0);
        var rep = await CreateRepAsync(ctx, org);
        SeedCartWithItem(ctx, "c1", org, quantity: 2);

        (await ReadCartQuantityAsync(ctx, org, rep)).Should().Be(2);

        SeedCartWithItem(ctx, "c2", org, quantity: 3);

        (await ReadCartQuantityAsync(ctx, org, rep)).Should().Be(5);
    }

    [Fact]
    public async Task CartStatistics_SurviveAnOrderChange()
    {
        const string org = "invalidation-cart-family-org";
        using var ctx = SalesRepTestContext.Create();
        var rep = await CreateRepAsync(ctx, org);
        SeedCartWithItem(ctx, "c1", org, quantity: 2);

        (await ReadCartQuantityAsync(ctx, org, rep)).Should().Be(2);

        SeedCartWithItem(ctx, "c2", org, quantity: 3);
        await PublishOrderChangedAsync(ctx, org);

        // Tokens are keyed by (family, organization): an order change concerns the order-driven families only.
        (await ReadCartQuantityAsync(ctx, org, rep)).Should().Be(2);
    }

    [Fact]
    public async Task OrderStatistics_AreServedFromTheCacheUntilTheOrderChangeArrives()
    {
        const string org = "invalidation-order-org";
        using var ctx = SalesRepTestContext.Create();
        var rep = await CreateRepAsync(ctx, org);
        SeedOrder(ctx, "o1", org, total: 100m);

        (await ReadOrderTotalAsync(ctx, org, rep)).Should().Be(100m);

        SeedOrder(ctx, "o2", org, total: 250m);
        (await ReadOrderTotalAsync(ctx, org, rep)).Should().Be(100m);

        await PublishOrderChangedAsync(ctx, org);

        (await ReadOrderTotalAsync(ctx, org, rep)).Should().Be(350m);
    }

    [Fact]
    public async Task OrderStatistics_SurviveAnOrderChangeThatNoAggregateReads()
    {
        const string org = "invalidation-order-delta-org";
        using var ctx = SalesRepTestContext.Create();
        var rep = await CreateRepAsync(ctx, org);
        SeedOrder(ctx, "o1", org, total: 100m);

        (await ReadOrderTotalAsync(ctx, org, rep)).Should().Be(100m);
        SeedOrder(ctx, "o2", org, total: 250m);

        // The status pipeline saves an order repeatedly; a save that moves nothing an aggregate reads keeps the entry.
        var before = NewOrder(org, total: 100m, status: "New");
        var afterComment = NewOrder(org, total: 100m, status: "New");
        afterComment.Comment = "called the customer back";
        await PublishOrderChangedAsync(ctx, before, afterComment);
        (await ReadOrderTotalAsync(ctx, org, rep)).Should().Be(100m);

        var afterStatus = NewOrder(org, total: 100m, status: "Processing");
        await PublishOrderChangedAsync(ctx, before, afterStatus);

        (await ReadOrderTotalAsync(ctx, org, rep)).Should().Be(350m);
    }

    [Fact]
    public async Task OrderStatistics_AreEvictedByALineItemChange()
    {
        const string org = "invalidation-order-lines-org";
        using var ctx = SalesRepTestContext.Create();
        var rep = await CreateRepAsync(ctx, org);
        SeedOrder(ctx, "o1", org, total: 100m);

        (await ReadOrderTotalAsync(ctx, org, rep)).Should().Be(100m);
        SeedOrder(ctx, "o2", org, total: 250m);

        // The top-seller ranking aggregates the line items, so their signature is part of what the families read.
        var before = NewOrder(org, total: 100m, status: "New");
        before.Items = [NewLineItem(quantity: 1)];
        var after = NewOrder(org, total: 100m, status: "New");
        after.Items = [NewLineItem(quantity: 4)];

        await PublishOrderChangedAsync(ctx, before, after);

        (await ReadOrderTotalAsync(ctx, org, rep)).Should().Be(350m);
    }

    [Fact]
    public async Task SoldCategories_SurviveAnOrderChangeWhileTopSellersAreTtlOnly()
    {
        const string org = "invalidation-topseller-default-org";
        using var ctx = SalesRepTestContext.Create();
        var rep = await CreateRepAsync(ctx, org);
        SeedOrder(ctx, "o1", org, total: 100m, categoryId: "cat-1");

        (await ReadSoldCategoriesAsync(ctx, org, rep)).Should().BeEquivalentTo(["cat-1"]);

        SeedOrder(ctx, "o2", org, total: 100m, categoryId: "cat-2");
        await PublishOrderChangedAsync(ctx, org);

        // The top-seller family defaults to TTL-only: the heaviest query, and no acceptance criterion needs it fresh.
        (await ReadSoldCategoriesAsync(ctx, org, rep)).Should().BeEquivalentTo(["cat-1"]);
    }

    [Fact]
    public async Task SoldCategories_AreEvictedByAnOrderChangeWhenTheTopSellerFlagIsOn()
    {
        const string org = "invalidation-topseller-flag-org";
        using var ctx = SalesRepTestContext.Create();
        ctx.SetSetting(ModuleConstants.Settings.Caching.TopSellerInvalidateOnChange, true);
        var rep = await CreateRepAsync(ctx, org);
        SeedOrder(ctx, "o1", org, total: 100m, categoryId: "cat-1");

        (await ReadSoldCategoriesAsync(ctx, org, rep)).Should().BeEquivalentTo(["cat-1"]);

        SeedOrder(ctx, "o2", org, total: 100m, categoryId: "cat-2");
        await PublishOrderChangedAsync(ctx, org);

        (await ReadSoldCategoriesAsync(ctx, org, rep)).Should().BeEquivalentTo(["cat-1", "cat-2"]);
    }

    [Fact]
    public async Task CustomersList_InlinePurchaseFigures_AreEvictedByAnOrderChange()
    {
        const string org = "invalidation-customers-list-org";
        using var ctx = SalesRepTestContext.Create();
        var rep = await CreateRepAsync(ctx, org);
        SeedOrder(ctx, "o1", org, total: 100m);

        (await ReadCustomerPurchasesAsync(ctx, rep)).Should().Be(100m);

        SeedOrder(ctx, "o2", org, total: 250m);
        (await ReadCustomerPurchasesAsync(ctx, rep)).Should().Be(100m);

        await PublishOrderChangedAsync(ctx, org);

        // The customers list's inline figures and ordering route through the same per-organization aggregate as the
        // hub statistics, so they go fresh with it — the rows were never the stale part.
        (await ReadCustomerPurchasesAsync(ctx, rep)).Should().Be(350m);
    }

    private static async Task<SalesRepDetails> CreateRepAsync(SalesRepTestContext ctx, params string[] organizationIds)
    {
        await ctx.SeedOrganizationsAsync(organizationIds);
        return await ctx.CreateRepAsync("Jane", "Rep", $"jane-{organizationIds[0]}@test.com", organizationIds);
    }

    private static async Task<int> ReadCartQuantityAsync(SalesRepTestContext ctx, string org, SalesRepDetails rep)
    {
        var json = await ctx.ExecuteGraphQlAsync(CartQuery.Replace("{org}", org), userId: rep.UserId);

        return SalesRepTestContext.Node(json, "salesRepCustomerCartStatistics")
            .GetProperty("lifetime").GetProperty("selectedItemQuantity").GetInt32();
    }

    private static async Task<decimal> ReadOrderTotalAsync(SalesRepTestContext ctx, string org, SalesRepDetails rep)
    {
        var json = await ctx.ExecuteGraphQlAsync(OrderQuery.Replace("{org}", org), userId: rep.UserId);

        return SalesRepTestContext.Node(json, "salesRepCustomerOrderStatistics")
            .GetProperty("lifetime").GetProperty("total").GetProperty("amount").GetDecimal();
    }

    private static async Task<decimal> ReadCustomerPurchasesAsync(SalesRepTestContext ctx, SalesRepDetails rep)
    {
        var json = await ctx.ExecuteGraphQlAsync(
            """
            query { salesRepCustomers { items { organizationId
              ytd: orderStatistics(from:"2026-01-01T00:00:00Z", to:"2026-12-31T00:00:00Z") { total { amount } }
            } } }
            """,
            userId: rep.UserId);

        return SalesRepTestContext.Node(json, "salesRepCustomers").GetProperty("items").EnumerateArray().Single()
            .GetProperty("ytd").GetProperty("total").GetProperty("amount").GetDecimal();
    }

    private static async Task<IList<string>> ReadSoldCategoriesAsync(SalesRepTestContext ctx, string org, SalesRepDetails rep)
    {
        var criteria = SalesRepScopeCriteria.Create([org], rep.UserId, "B2B-store", null, null);

        return await ctx.GetRequiredService<ISalesRepTopSellerService>().GetSoldCategoryIdsAsync(criteria);
    }

    private static Task PublishCartChangedAsync(SalesRepTestContext ctx, string org)
    {
        var cart = AbstractTypeFactory<ShoppingCart>.TryCreateInstance();
        cart.Id = "changed-cart";
        cart.OrganizationId = org;

        return ctx.GetRequiredService<IEventPublisher>()
            .Publish(new CartChangedEvent([new GenericChangedEntry<ShoppingCart>(cart, EntryState.Modified)]));
    }

    private static Task PublishOrderChangedAsync(SalesRepTestContext ctx, string org)
    {
        return PublishOrderChangedAsync(ctx, null, NewOrder(org, total: 1m, status: "New"));
    }

    private static Task PublishOrderChangedAsync(SalesRepTestContext ctx, CustomerOrder oldEntry, CustomerOrder newEntry)
    {
        var entry = oldEntry == null
            ? new GenericChangedEntry<CustomerOrder>(newEntry, EntryState.Added)
            : new GenericChangedEntry<CustomerOrder>(newEntry, oldEntry, EntryState.Modified);

        return ctx.GetRequiredService<IEventPublisher>().Publish(new OrderChangedEvent([entry]));
    }

    private static CustomerOrder NewOrder(string org, decimal total, string status)
    {
        var order = AbstractTypeFactory<CustomerOrder>.TryCreateInstance();
        order.Id = "changed-order";
        order.OrganizationId = org;
        order.StoreId = "B2B-store";
        order.Currency = "USD";
        order.Total = total;
        order.Status = status;
        order.CreatedDate = _feb2026;
        return order;
    }

    private static OrderLineItem NewLineItem(int quantity)
    {
        var lineItem = AbstractTypeFactory<OrderLineItem>.TryCreateInstance();
        lineItem.Id = "changed-order-li";
        lineItem.ProductId = "product-1";
        lineItem.Currency = "USD";
        lineItem.Price = 10m;
        lineItem.Quantity = quantity;
        return lineItem;
    }

    private static void SeedCartWithItem(SalesRepTestContext ctx, string id, string org, int quantity)
    {
        using var db = ctx.NewCartDbContext();
        db.Add(new ShoppingCartEntity
        {
            Id = id,
            Name = CartModuleConstants.DefaultCartName,
            CheckoutId = id, // [Required] on ShoppingCartEntity
            OrganizationId = org,
            CustomerId = ctx.LastCreatedRepUserId,
            CustomerName = "Customer 1",
            StoreId = "B2B-store",
            Currency = "USD",
            CreatedDate = _feb2026,
            ModifiedDate = _feb2026,
            LineItemsCount = 1,
        });
        db.Add(new CartLineItemEntity
        {
            Id = $"{id}-item",
            ShoppingCartId = id,
            ProductId = $"{id}-product",
            CatalogId = "catalog-1",
            Sku = id,
            Name = id,
            Currency = "USD",
            Quantity = quantity,
            SelectedForCheckout = true,
            ListPrice = 10m,
            CreatedDate = _feb2026,
            ModifiedDate = _feb2026,
        });
        db.SaveChanges();
    }

    private static void SeedOrder(
        SalesRepTestContext ctx, string id, string org, decimal total, string categoryId = null)
    {
        using var db = ctx.NewOrderDbContext();
        var order = new CustomerOrderEntity
        {
            Id = id,
            Number = id,
            OrganizationId = org,
            CustomerId = ctx.LastCreatedRepUserId,
            CustomerName = "Customer 1",
            StoreId = "B2B-store",
            Status = "New",
            Currency = "USD",
            Total = total,
            CreatedDate = _feb2026,
            ModifiedDate = _feb2026,
        };

        if (categoryId != null)
        {
            order.Items.Add(new OrderLineItemEntity
            {
                Id = $"{id}-li",
                ProductId = $"{id}-product",
                CatalogId = "catalog-1",
                CategoryId = categoryId,
                Sku = $"SKU-{id}",
                Name = $"Product {id}",
                ProductType = "Physical",
                Quantity = 1,
                Price = total,
                Currency = "USD",
                CreatedDate = _feb2026,
                ModifiedDate = _feb2026,
            });
        }

        db.Add(order);
        db.SaveChanges();
    }
}
