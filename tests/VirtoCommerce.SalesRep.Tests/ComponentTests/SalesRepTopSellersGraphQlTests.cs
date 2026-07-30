using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// End-to-end component tests for the <c>salesRepTopSellers</c> X-API query (dashboard + customer "Top Sellers"):
/// seed real order line items into in-memory SQLite and assert the ranking (units / revenue), period scoping, the
/// category filter (real <c>CategorySearchService</c> + real filter resolver over a real catalog slice; category →
/// product ids resolved by the repo-backed <c>IProductIndexedSearchService</c> stand-in, restricting the ranking to
/// products in the selected subtree), the take cap and the data-isolation invariant. No mocks — the real
/// aggregation, sort and category-filter services run.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepTopSellersGraphQlTests
{
    private static readonly DateTime _date = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SalesRepTopSellers_RanksByUnitsByDefault()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedProductLine(ctx, "o-a", "org-1", "prodA", quantity: 10, price: 5m);  // units 10, revenue 50
        SeedProductLine(ctx, "o-b", "org-1", "prodB", quantity: 3, price: 20m);  // units 3,  revenue 60
        SeedProductLine(ctx, "o-c", "org-1", "prodC", quantity: 7, price: 1m);   // units 7,  revenue 7

        var items = TopSellers(await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellers { rank productId units revenue { amount } } }",
            userId: rep.UserId));

        // Default ordering is by units desc: prodA (10) → prodC (7) → prodB (3).
        items.Select(x => x.GetProperty("productId").GetString()).Should().Equal("prodA", "prodC", "prodB");
        items[0].GetProperty("rank").GetInt32().Should().Be(1);
        items[0].GetProperty("units").GetInt32().Should().Be(10);
        items[0].GetProperty("revenue").GetProperty("amount").GetDecimal().Should().Be(50m);
    }

    [Fact]
    public async Task SalesRepTopSellers_SortByRevenue_ReordersByRevenue()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedProductLine(ctx, "o-a", "org-1", "prodA", quantity: 10, price: 5m);  // revenue 50
        SeedProductLine(ctx, "o-b", "org-1", "prodB", quantity: 3, price: 20m);  // revenue 60
        SeedProductLine(ctx, "o-c", "org-1", "prodC", quantity: 7, price: 1m);   // revenue 7

        var items = TopSellers(await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellers(sort: \"by-revenue\") { productId revenue { amount } } }",
            userId: rep.UserId));

        // By revenue desc: prodB (60) → prodA (50) → prodC (7).
        items.Select(x => x.GetProperty("productId").GetString()).Should().Equal("prodB", "prodA", "prodC");
        items[0].GetProperty("revenue").GetProperty("amount").GetDecimal().Should().Be(60m);
    }

    [Fact]
    public async Task SalesRepTopSellers_Period_ScopesByCreatedDate()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedProductLine(ctx, "o-in", "org-1", "prodIn", quantity: 5, price: 10m, createdDate: new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        SeedProductLine(ctx, "o-out", "org-1", "prodOut", quantity: 9, price: 10m, createdDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var items = TopSellers(await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellers(period: { from: \"2026-06-01T00:00:00Z\", to: \"2026-07-01T00:00:00Z\" }) { productId } }",
            userId: rep.UserId));

        items.Select(x => x.GetProperty("productId").GetString()).Should().Equal("prodIn");
    }

    [Fact]
    public async Task SalesRepTopSellers_CategoryFilter_RestrictsToSubtree()
    {
        using var ctx = SalesRepTestContext.Create();
        // Electronics (top-level) → Printers (nested); Apparel (top-level).
        await ctx.SeedCategoriesAsync(
            ("cat-electronics", "Electronics", null, true),
            ("cat-printers", "Printers", "cat-electronics", true),
            ("cat-apparel", "Apparel", null, true));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedProductLine(ctx, "o-p", "org-1", "prodPrinter", quantity: 5, price: 10m, categoryId: "cat-printers"); // nested under electronics
        SeedProductLine(ctx, "o-s", "org-1", "prodShirt", quantity: 8, price: 10m, categoryId: "cat-apparel");
        // Catalog products (with their real category) so the index-backed filter can resolve subtree membership —
        // the line-item CategoryId is not what the (option (a)) filter matches on.
        await ctx.SeedProductsAsync(("prodPrinter", "cat-printers"), ("prodShirt", "cat-apparel"));

        // Filtering by the top-level "electronics" must catch the product filed under the nested "printers".
        var items = TopSellers(await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellers(storeId: \"B2B-store\", filter: \"cat-electronics\") { productId } }",
            userId: rep.UserId));

        items.Select(x => x.GetProperty("productId").GetString()).Should().Equal("prodPrinter");
    }

    [Fact]
    public async Task SalesRepTopSellers_CategoryFilter_ExpandsFullSubtreeRecursively()
    {
        using var ctx = SalesRepTestContext.Create();
        // 3-level tree: Electronics → Printers → Laser printers, plus a sibling top-level Apparel.
        await ctx.SeedCategoriesAsync(
            ("cat-electronics", "Electronics", null, true),
            ("cat-printers", "Printers", "cat-electronics", true),
            ("cat-laser", "Laser printers", "cat-printers", true),
            ("cat-apparel", "Apparel", null, true));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedProductLine(ctx, "o-l", "org-1", "prodLaser", quantity: 5, price: 10m, categoryId: "cat-laser");     // 2 levels deep
        SeedProductLine(ctx, "o-p", "org-1", "prodPrinter", quantity: 4, price: 10m, categoryId: "cat-printers"); // 1 level deep
        SeedProductLine(ctx, "o-s", "org-1", "prodShirt", quantity: 8, price: 10m, categoryId: "cat-apparel");    // outside the subtree
        // Catalog products (with their real category) so the index-backed filter resolves the full recursive subtree.
        await ctx.SeedProductsAsync(("prodLaser", "cat-laser"), ("prodPrinter", "cat-printers"), ("prodShirt", "cat-apparel"));

        // Filtering by the top-level "electronics" must catch descendants at ANY depth (printers AND laser
        // printers), but not the sibling apparel — the filter resolves to the whole recursive subtree of ids.
        var items = TopSellers(await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellers(storeId: \"B2B-store\", filter: \"cat-electronics\") { productId } }",
            userId: rep.UserId));

        // Default by-units desc: laser (5) → printer (4); apparel (8) is excluded despite the higher count.
        items.Select(x => x.GetProperty("productId").GetString()).Should().Equal("prodLaser", "prodPrinter");
    }

    [Fact]
    public async Task SalesRepTopSellers_UnrecognizedCategory_FailsClosed()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedCategoriesAsync(("cat-electronics", "Electronics", null, true));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedProductLine(ctx, "o-a", "org-1", "prodA", quantity: 5, price: 10m, categoryId: "cat-electronics");

        var items = TopSellers(await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellers(storeId: \"B2B-store\", filter: \"cat-nonexistent\") { productId } }",
            userId: rep.UserId));

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task SalesRepTopSellers_Take_ClampedToMax10()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        for (var i = 1; i <= 12; i++)
        {
            SeedProductLine(ctx, $"o-{i:D2}", "org-1", $"prod{i:D2}", quantity: i, price: 1m);
        }

        var items = TopSellers(await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellers(take: 50) { productId } }",
            userId: rep.UserId));

        items.Should().HaveCount(10); // take is clamped to the 10 max
    }

    [Fact]
    public async Task SalesRepTopSellers_ExposesProductNameSkuAndImageUrl()
    {
        // The dashboard's Top Sellers row renders name, sku and the product image — all carried by the order line
        // item and grouped by product. Assert every display field the frontend selects is surfaced.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedProductLine(ctx, "o-a", "org-1", "prodA", quantity: 10, price: 5m, imageUrl: "catalog/prodA/image.jpg");

        var items = TopSellers(await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellers { productId name sku imageUrl } }",
            userId: rep.UserId));

        items.Should().HaveCount(1);
        items[0].GetProperty("productId").GetString().Should().Be("prodA");
        items[0].GetProperty("name").GetString().Should().Be("Product prodA");
        items[0].GetProperty("sku").GetString().Should().Be("SKU-prodA");
        items[0].GetProperty("imageUrl").GetString().Should().Be("catalog/prodA/image.jpg");
    }

    [Fact]
    public async Task SalesRepTopSellers_VaryingSnapshotAcrossOrders_ShowsLatestOrdersDisplay()
    {
        // The same product sold twice with a different image snapshot: units sum across both, but the display fields
        // must come from the MOST RECENT order — deterministically, not from an arbitrary grouped row.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedProductLine(ctx, "o-old", "org-1", "prodA", quantity: 5, price: 10m,
            createdDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), imageUrl: "old.jpg");
        SeedProductLine(ctx, "o-new", "org-1", "prodA", quantity: 3, price: 10m,
            createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), imageUrl: "new.jpg");

        var items = TopSellers(await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellers { productId units imageUrl } }",
            userId: rep.UserId));

        items.Should().HaveCount(1);
        items[0].GetProperty("units").GetInt32().Should().Be(8);          // summed across both snapshots
        items[0].GetProperty("imageUrl").GetString().Should().Be("new.jpg"); // latest order's snapshot
    }

    [Fact]
    public async Task SalesRepTopSellers_ExcludesOtherRepsLineItems()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedProductLine(ctx, "mine", "org-1", "prodMine", quantity: 3, price: 10m);
        // Data-isolation invariant: a foreign rep's much larger sale in a served org must not appear.
        SeedProductLine(ctx, "foreign", "org-1", "prodForeign", quantity: 100, price: 10m, createdByUserId: "other-rep");

        var items = TopSellers(await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellers { productId } }",
            userId: rep.UserId));

        items.Select(x => x.GetProperty("productId").GetString()).Should().Equal("prodMine");
    }

    [Fact]
    public async Task SalesRepTopSellerSortRules_ExposesUnitsAndRevenue()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellerSortRules { name } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("by-units").And.Contain("by-revenue");
    }

    [Fact]
    public async Task SalesRepTopSellers_SortByUnitsAscending_ReturnsError()
    {
        // Top-seller orderings are one-way (highest first): ranking a "top" list ascending is meaningless, so an
        // explicit ":asc" is rejected rather than silently ignored.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellers(sort: \"by-units:asc\") { productId } }",
            userId: rep.UserId);

        json.Should().Contain("\"errors\"");
    }

    [Fact]
    public async Task SalesRepTopSellerFilterRules_ReturnsTopLevelActiveCategories()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedCategoriesAsync(
            ("cat-electronics", "Electronics", null, true),
            ("cat-printers", "Printers", "cat-electronics", true), // nested → not a badge
            ("cat-apparel", "Apparel", null, true),
            ("cat-hidden", "Hidden", null, false));                // inactive → not a badge
        // Electronics is populated only through its nested Printers category — a badge counts the whole subtree.
        await ctx.SeedProductsAsync(("prodPrinter", "cat-printers"), ("prodShirt", "cat-apparel"), ("prodHidden", "cat-hidden"));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellerFilterRules(storeId: \"B2B-store\") { name localizedName } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("cat-electronics").And.Contain("cat-apparel");
        json.Should().NotContain("cat-printers").And.NotContain("cat-hidden");
    }

    [Fact]
    public async Task SalesRepTopSellerFilterRules_OmitsCategoriesWithoutProducts()
    {
        // A top-level category whose subtree holds no product could only ever produce an empty Top Sellers list, so
        // it is not offered as a badge.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedCategoriesAsync(
            ("cat-electronics", "Electronics", null, true),
            ("cat-printers", "Printers", "cat-electronics", true),
            ("cat-empty", "Empty", null, true));
        await ctx.SeedProductsAsync(("prodPrinter", "cat-printers"));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellerFilterRules(storeId: \"B2B-store\") { name } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("cat-electronics");   // populated via its nested category
        json.Should().NotContain("cat-empty");
    }

    [Fact]
    public async Task SalesRepTopSellerFilterRules_NonRepCaller_ReturnsEmpty()
    {
        // Authorization: rule discovery is sales-rep-only. Even with a rep and categories fully set up, a merely-
        // authenticated caller with no granting membership (a regular B2B buyer) must not enumerate the vocabulary —
        // which for the top-seller filter rules would leak the store's top-level catalog category IDs/names.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedCategoriesAsync(
            ("cat-electronics", "Electronics", null, true),
            ("cat-apparel", "Apparel", null, true));
        await ctx.SeedProductsAsync(("prodTv", "cat-electronics"), ("prodShirt", "cat-apparel"));
        await ctx.SeedOrganizationsAsync("org-1");
        await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellerFilterRules(storeId: \"B2B-store\") { name } }",
            userId: "regular-buyer");

        json.Should().NotContain("\"errors\"");
        json.Should().NotContain("cat-electronics").And.NotContain("cat-apparel");
    }

    [Fact]
    public async Task SalesRepTopSellers_UnconfiguredCurrency_SurfacesCurrencyOnlyWarning()
    {
        // #4, top-seller branch: revenue is folded per product from line-item snapshots, and those groups carry no
        // record count (only an amount). A product sold in an unconfigured currency (GBP — harness knows USD + EUR)
        // therefore yields a currency-only warning (no "N records"), and its Revenue is the partial converted amount.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedProductLine(ctx, "o-usd", "org-1", "prodUsd", quantity: 2, price: 10m, currency: "USD"); // revenue 20, convertible
        SeedProductLine(ctx, "o-gbp", "org-1", "prodGbp", quantity: 5, price: 100m, currency: "GBP"); // unconfigured → excluded

        var items = TopSellers(await ctx.ExecuteGraphQlAsync(
            "query { salesRepTopSellers(currencyCode: \"USD\") { productId units revenue { amount } warning } }",
            userId: rep.UserId));

        var usd = items.Single(x => x.GetProperty("productId").GetString() == "prodUsd");
        usd.GetProperty("revenue").GetProperty("amount").GetDecimal().Should().Be(20m);
        usd.GetProperty("warning").ValueKind.Should().Be(JsonValueKind.Null); // fully convertible → no warning

        var gbp = items.Single(x => x.GetProperty("productId").GetString() == "prodGbp");
        gbp.GetProperty("units").GetInt32().Should().Be(5);                 // units still counted from the snapshot
        gbp.GetProperty("revenue").GetProperty("amount").GetDecimal().Should().Be(0m); // GBP amount could not be converted
        var warning = gbp.GetProperty("warning");
        warning.ValueKind.Should().Be(JsonValueKind.String);
        warning.GetString().Should().Contain("GBP");
    }

    // ---- helpers ----

    private static JsonElement[] TopSellers(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("errors", out _).Should().BeFalse("GraphQL response should carry no errors: {0}", json);
        return root.GetProperty("data").GetProperty("salesRepTopSellers").EnumerateArray().Select(x => x.Clone()).ToArray();
    }

    private static void SeedProductLine(
        SalesRepTestContext ctx, string orderId, string org, string productId,
        int quantity, decimal price, DateTime? createdDate = null,
        string categoryId = "cat-default", string createdByUserId = null, string imageUrl = null,
        string currency = "USD")
    {
        var date = createdDate ?? _date;

        using var db = ctx.NewOrderDbContext();
        var order = new CustomerOrderEntity
        {
            Id = orderId,
            Number = orderId,
            OrganizationId = org,
            CustomerId = createdByUserId ?? ctx.LastCreatedRepUserId ?? "customer-1",
            CustomerName = "Customer 1",
            StoreId = "B2B-store",
            Status = "New",
            Currency = currency,
            Total = price * quantity,
            IsPrototype = false,
            CreatedDate = date,
            ModifiedDate = date,
        };
        order.Items.Add(new LineItemEntity
        {
            Id = $"{orderId}-li",
            ProductId = productId,
            CatalogId = "catalog-1",
            CategoryId = categoryId,
            Sku = $"SKU-{productId}",
            Name = $"Product {productId}",
            ImageUrl = imageUrl,
            ProductType = "Physical",
            Quantity = quantity,
            Price = price,
            Currency = currency,
            CreatedDate = date,
            ModifiedDate = date,
        });

        db.Add(order);
        db.SaveChanges();
    }
}
