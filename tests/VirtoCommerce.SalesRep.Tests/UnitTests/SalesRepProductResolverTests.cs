using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The analytics product code -> catalog product lookup. Analytics carries a code, not an id, so this is the only
/// thing standing between a tracked product view and a raw SKU on the rep's screen.
/// </summary>
public class SalesRepProductResolverTests
{
    private const string StoreId = "B2B-store";
    private const string CatalogId = "physical-catalog";

    // GA sends the variation's own code as item_id whenever the storefront sells by size or pack, and a product
    // search excludes variations unless it is asked for them.
    [Fact]
    public async Task ResolveAsync_AsksForVariations()
    {
        var search = new FakeProductSearchService();
        var resolver = CreateResolver(search, CatalogId);

        await ResolveOneAsync(resolver, "SKU-RED-M");

        search.LastCriteria.SearchInVariations.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveAsync_VariationCode_Resolves()
    {
        var search = new FakeProductSearchService(Product("v1", "SKU-RED-M", "Red shirt, M"));
        var resolver = CreateResolver(search, CatalogId);

        var row = await ResolveOneAsync(resolver, "SKU-RED-M");

        row.Product.Should().NotBeNull();
        row.Product.ProductId.Should().Be("v1");
        row.Product.Name.Should().Be("Red shirt, M");
    }

    // One code carried by two catalogs used to blank the WHOLE page: Take was one row per code, so the extra row
    // pushed TotalCount over it and the batch gave up. The contract is per row.
    [Fact]
    public async Task ResolveAsync_OneAmbiguousCode_LeavesOnlyThatRowUnresolved()
    {
        var search = new FakeProductSearchService(
            Product("p1", "SKU-1", "First"),
            Product("p2", "SKU-2", "Second"),
            Product("p3", "SKU-2", "Second, other catalog"),
            Product("p4", "SKU-3", "Third"));
        var resolver = CreateResolver(search, catalogId: null);

        var rows = await ResolveAsync(resolver, "SKU-1", "SKU-2", "SKU-3");

        rows[0].Product.ProductId.Should().Be("p1");
        rows[1].Product.Should().BeNull("a code carried by two catalogs cannot be attributed to either");
        rows[2].Product.ProductId.Should().Be("p4");
    }

    [Fact]
    public async Task ResolveAsync_PageCannotCarryEveryMatch_ResolvesNothing()
    {
        // More matches than the request could fetch: no code's match set is known, so none can be trusted.
        var search = new FakeProductSearchService(Product("p1", "SKU-1", "First")) { TotalCountOverride = 500 };
        var resolver = CreateResolver(search, catalogId: null);

        var rows = await ResolveAsync(resolver, "SKU-1");

        rows[0].Product.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_AsksForHeadroomOverOneRowPerCode()
    {
        var search = new FakeProductSearchService();
        var resolver = CreateResolver(search, catalogId: null);

        await ResolveAsync(resolver, "SKU-1", "SKU-2");

        search.LastCriteria.Take.Should().BeGreaterThan(2);
        search.LastCriteria.Skus.Should().BeEquivalentTo("SKU-1", "SKU-2");
    }

    [Fact]
    public async Task ResolveAsync_DuplicateAndEmptyCodes_AreAskedForOnce()
    {
        var search = new FakeProductSearchService(Product("p1", "SKU-1", "First"));
        var resolver = CreateResolver(search, CatalogId);

        var rows = await ResolveAsync(resolver, "SKU-1", "sku-1", "", null);

        search.LastCriteria.Skus.Should().BeEquivalentTo("SKU-1");
        rows[0].Product.ProductId.Should().Be("p1");
        rows[1].Product.ProductId.Should().Be("p1", "codes are matched ignoring case");
        rows[2].Product.Should().BeNull();
        rows[3].Product.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_NoCodes_DoesNotSearch()
    {
        var search = new FakeProductSearchService();
        var resolver = CreateResolver(search, CatalogId);

        await ResolveAsync(resolver, "", null);

        search.CallCount.Should().Be(0);
    }

    private static async Task<Row> ResolveOneAsync(SalesRepProductResolver resolver, string code)
    {
        return (await ResolveAsync(resolver, code))[0];
    }

    private static async Task<IList<Row>> ResolveAsync(SalesRepProductResolver resolver, params string[] codes)
    {
        var rows = codes.Select(x => new Row { Code = x }).ToList();

        await resolver.ResolveAsync(rows, StoreId, x => x.Code, (row, product) => row.Product = product);

        return rows;
    }

    private static SalesRepProductResolver CreateResolver(IProductSearchService productSearchService, string catalogId)
    {
        return new TestableProductResolver(productSearchService, catalogId);
    }

    private static CatalogProduct Product(string id, string code, string name)
    {
        return new CatalogProduct { Id = id, Code = code, Name = name };
    }

    private sealed class Row
    {
        public string Code { get; init; }

        public SalesRepActivityProduct Product { get; set; }
    }

    // The store -> catalog lookup is its own concern (and its own two services); this pins what the SEARCH is
    // asked for and how its rows are attributed.
    private sealed class TestableProductResolver : SalesRepProductResolver
    {
        private readonly string _catalogId;

        public TestableProductResolver(IProductSearchService productSearchService, string catalogId)
            : base(productSearchService, storeService: null, catalogService: null)
        {
            _catalogId = catalogId;
        }

        protected override Task<string> GetStoreCatalogIdAsync(string storeId)
        {
            return Task.FromResult(_catalogId);
        }
    }

    private sealed class FakeProductSearchService : IProductSearchService
    {
        private readonly IList<CatalogProduct> _products;

        public FakeProductSearchService(params CatalogProduct[] products)
        {
            _products = products;
        }

        public ProductSearchCriteria LastCriteria { get; private set; }

        public int CallCount { get; private set; }

        public int? TotalCountOverride { get; init; }

        public Task<ProductSearchResult> SearchAsync(ProductSearchCriteria criteria, bool clone = true)
        {
            LastCriteria = criteria;
            CallCount++;

            var matches = _products
                .Where(x => criteria.Skus.Contains(x.Code, StringComparer.OrdinalIgnoreCase))
                .Take(criteria.Take)
                .ToList();

            return Task.FromResult(new ProductSearchResult
            {
                TotalCount = TotalCountOverride ?? matches.Count,
                Results = matches,
            });
        }
    }
}
