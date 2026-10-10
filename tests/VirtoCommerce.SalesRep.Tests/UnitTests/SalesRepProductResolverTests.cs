using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Outlines;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

// Analytics carries a product code, not an id: this lookup is what stands between a tracked view and a raw SKU.
public class SalesRepProductResolverTests
{
    private const string StoreId = "B2B-store";
    private const string CatalogId = "physical-catalog";
    private const string VirtualCatalogId = "virtual-catalog";

    // GA sends the variation's own code as item_id, and a product search excludes variations unless asked.
    [Fact]
    public async Task ResolveAsync_AsksForVariations()
    {
        var search = new FakeProductSearchService();
        var resolver = CreateResolver(search, PhysicalCatalog());

        await ResolveOneAsync(resolver, "SKU-RED-M");

        search.LastCriteria.SearchInVariations.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveAsync_VariationCode_Resolves()
    {
        var search = new FakeProductSearchService(Product("v1", "SKU-RED-M", "Red shirt, M"));
        var resolver = CreateResolver(search, PhysicalCatalog());

        var row = await ResolveOneAsync(resolver, "SKU-RED-M");

        row.Product.Should().NotBeNull();
        row.Product.ProductId.Should().Be("v1");
        row.Product.Name.Should().Be("Red shirt, M");
    }

    // One code carried by two catalogs used to blank the WHOLE page. The contract is per row.
    [Fact]
    public async Task ResolveAsync_OneAmbiguousCode_LeavesOnlyThatRowUnresolved()
    {
        var search = new FakeProductSearchService(
            Product("p1", "SKU-1", "First"),
            Product("p2", "SKU-2", "Second"),
            Product("p3", "SKU-2", "Second, other catalog"),
            Product("p4", "SKU-3", "Third"));
        var resolver = CreateResolver(search, catalog: null);

        var rows = await ResolveAsync(resolver, "SKU-1", "SKU-2", "SKU-3");

        rows[0].Product.ProductId.Should().Be("p1");
        rows[1].Product.Should().BeNull("a code carried by two catalogs cannot be attributed to either");
        rows[2].Product.ProductId.Should().Be("p4");
    }

    [Fact]
    public async Task ResolveAsync_PageCannotCarryEveryMatch_ResolvesNothing()
    {
        // More matches than the request could fetch: no code's match set is known.
        var search = new FakeProductSearchService(Product("p1", "SKU-1", "First")) { TotalCountOverride = 500 };
        var resolver = CreateResolver(search, catalog: null);

        var rows = await ResolveAsync(resolver, "SKU-1");

        rows[0].Product.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_AsksForHeadroomOverOneRowPerCode()
    {
        var search = new FakeProductSearchService();
        var resolver = CreateResolver(search, catalog: null);

        await ResolveAsync(resolver, "SKU-1", "SKU-2");

        search.LastCriteria.Take.Should().BeGreaterThan(2);
        search.LastCriteria.Skus.Should().BeEquivalentTo("SKU-1", "SKU-2");
    }

    [Fact]
    public async Task ResolveAsync_DuplicateAndEmptyCodes_AreAskedForOnce()
    {
        var search = new FakeProductSearchService(Product("p1", "SKU-1", "First"));
        var resolver = CreateResolver(search, PhysicalCatalog());

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
        var resolver = CreateResolver(search, PhysicalCatalog());

        await ResolveAsync(resolver, "", null);

        search.CallCount.Should().Be(0);
    }

    // B2B-store's setup: a virtual catalog links products in from physical ones, and two physical catalogs may carry
    // the same code. The customer viewed the one THIS store shows.
    [Fact]
    public async Task ResolveAsync_VirtualStoreCatalog_AmbiguousCode_ResolvesToTheProductTheStoreShows()
    {
        var search = new FakeProductSearchService(
            Product("p2", "SKU-2", "Second"),
            Product("p3", "SKU-2", "Second, other catalog"));
        var items = new FakeItemService("p2");
        var resolver = CreateResolver(search, VirtualCatalog(), items);

        var row = await ResolveOneAsync(resolver, "SKU-2");

        row.Product.ProductId.Should().Be("p2");
        items.LastIds.Should().BeEquivalentTo("p2", "p3");
        items.LastCatalogId.Should().Be(VirtualCatalogId);
        search.LastCriteria.CatalogId.Should().BeNull("a virtual catalog holds links, so narrowing by it finds nothing");
    }

    [Fact]
    public async Task ResolveAsync_VirtualStoreCatalog_AmbiguousCodeTheStoreDoesNotShow_StaysUnresolved()
    {
        var search = new FakeProductSearchService(
            Product("p2", "SKU-2", "Second"),
            Product("p3", "SKU-2", "Second, other catalog"));
        var resolver = CreateResolver(search, VirtualCatalog(), new FakeItemService());

        var row = await ResolveOneAsync(resolver, "SKU-2");

        row.Product.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_VirtualStoreCatalog_AmbiguousCodeTheStoreShowsTwice_StaysUnresolved()
    {
        var search = new FakeProductSearchService(
            Product("p2", "SKU-2", "Second"),
            Product("p3", "SKU-2", "Second, other catalog"));
        var resolver = CreateResolver(search, VirtualCatalog(), new FakeItemService("p2", "p3"));

        var row = await ResolveOneAsync(resolver, "SKU-2");

        row.Product.Should().BeNull("the store shows both, so neither is the one the customer viewed");
    }

    // Outlines cost a product load, so only a code with more than one match pays for them.
    [Fact]
    public async Task ResolveAsync_VirtualStoreCatalog_UniqueCodes_LoadNoOutlines()
    {
        var search = new FakeProductSearchService(Product("p1", "SKU-1", "First"));
        var items = new FakeItemService();
        var resolver = CreateResolver(search, VirtualCatalog(), items);

        var row = await ResolveOneAsync(resolver, "SKU-1");

        row.Product.ProductId.Should().Be("p1");
        items.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ResolveAsync_PhysicalStoreCatalog_NarrowsTheSearch()
    {
        var search = new FakeProductSearchService(Product("p1", "SKU-1", "First"));
        var items = new FakeItemService();
        var resolver = CreateResolver(search, PhysicalCatalog(), items);

        await ResolveOneAsync(resolver, "SKU-1");

        search.LastCriteria.CatalogId.Should().Be(CatalogId);
        items.CallCount.Should().Be(0);
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

    private static SalesRepProductResolver CreateResolver(IProductSearchService productSearchService, Catalog catalog, IItemService itemService = null)
    {
        return new TestableProductResolver(productSearchService, catalog, itemService ?? new FakeItemService());
    }

    private static Catalog PhysicalCatalog()
    {
        return new Catalog { Id = CatalogId, IsVirtual = false };
    }

    private static Catalog VirtualCatalog()
    {
        return new Catalog { Id = VirtualCatalogId, IsVirtual = true };
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

    // The store -> catalog lookup is its own concern; this pins what the SEARCH is asked and how rows attribute.
    private sealed class TestableProductResolver : SalesRepProductResolver
    {
        private readonly Catalog _catalog;

        public TestableProductResolver(IProductSearchService productSearchService, Catalog catalog, IItemService itemService)
            : base(productSearchService, storeService: null, catalogService: null, itemService)
        {
            _catalog = catalog;
        }

        protected override Task<string> GetStoreCatalogIdAsync(string storeId)
        {
            return Task.FromResult(_catalog?.Id);
        }

        protected override Task<bool> IsVirtualCatalogAsync(string catalogId)
        {
            return Task.FromResult(_catalog?.IsVirtual == true);
        }
    }

    // What the item service answers for outlines in a catalog: the products it was told the catalog shows get one.
    private sealed class FakeItemService : IItemService
    {
        private readonly ISet<string> _shownIds;

        public FakeItemService(params string[] shownIds)
        {
            _shownIds = shownIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public IList<string> LastIds { get; private set; }

        public string LastCatalogId { get; private set; }

        public int CallCount { get; private set; }

        public Task<IList<CatalogProduct>> GetByIdsAsync(IList<string> ids, string responseGroup, string catalogId)
        {
            LastIds = ids;
            LastCatalogId = catalogId;
            CallCount++;

            IList<CatalogProduct> result = ids
                .Select(id => new CatalogProduct { Id = id, Outlines = _shownIds.Contains(id) ? [new Outline()] : [] })
                .ToList();

            return Task.FromResult(result);
        }

        public Task<IList<CatalogProduct>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true) => throw new NotSupportedException();
        public Task<CatalogProduct> GetByIdAsync(string itemId, string responseGroup, string catalogId) => throw new NotSupportedException();
        public Task<IList<CatalogProduct>> GetByCodes(string catalogId, IList<string> codes, string responseGroup) => throw new NotSupportedException();
        public Task<IDictionary<string, string>> GetIdsByCodes(string catalogId, IList<string> codes) => throw new NotSupportedException();
        public Task<IList<CatalogProduct>> GetByOuterIdsAsync(IList<string> outerIds, string responseGroup = null, bool clone = true) => throw new NotSupportedException();
        public Task SaveChangesAsync(IList<CatalogProduct> models) => throw new NotSupportedException();
        public Task DeleteAsync(IList<string> ids, bool softDelete = false) => throw new NotSupportedException();
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
