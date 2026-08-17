using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// The metadata <see cref="ISalesRepDocumentMetadataSearchService"/> (platform SearchService base) over the real
/// module DbContext: its BuildQuery predicates (category ==, isPinned, keyword over the metadata Name, objectIds)
/// and the pinned-first default sort. This is the metadata half only — the document search orchestrator composes
/// it with the AssetEntry side and applies the file-name keyword + cross-table sort in memory.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepDocumentMetadataSearchTests
{
    [Fact]
    public async Task BuildQuery_FiltersByCategoryPinAndObjectIds()
    {
        using var ctx = SalesRepTestContext.Create();
        var catalog = await UploadAsync(ctx, "Spring Catalog.pdf", "Catalogs");
        var lookbook = await UploadAsync(ctx, "Lookbook.pdf", "Lookbooks");
        var pricing = await UploadAsync(ctx, "Price List.xlsx", "Pricing");

        var searchService = ctx.GetRequiredService<ISalesRepDocumentMetadataSearchService>();
        var metadataService = ctx.GetRequiredService<ISalesRepDocumentMetadataService>();
        await metadataService.SetPinnedAsync(pricing.Id, isPinned: true);

        // Everything.
        var all = await searchService.SearchAsync(new SalesRepDocumentMetadataSearchCriteria { Take = 100 });
        all.TotalCount.Should().Be(3);

        // Category == (exact).
        var byCategory = await searchService.SearchAsync(new SalesRepDocumentMetadataSearchCriteria { Category = "Catalogs", Take = 100 });
        byCategory.Results.Select(x => x.Id).Should().Equal(catalog.Id);

        // Pin flag.
        var pinned = await searchService.SearchAsync(new SalesRepDocumentMetadataSearchCriteria { IsPinned = true, Take = 100 });
        pinned.Results.Select(x => x.Id).Should().Equal(pricing.Id);

        var unpinned = await searchService.SearchAsync(new SalesRepDocumentMetadataSearchCriteria { IsPinned = false, Take = 100 });
        unpinned.Results.Select(x => x.Id).Should().BeEquivalentTo([catalog.Id, lookbook.Id]);

        // ObjectIds.
        var byIds = await searchService.SearchAsync(new SalesRepDocumentMetadataSearchCriteria { ObjectIds = [catalog.Id, pricing.Id], Take = 100 });
        byIds.Results.Select(x => x.Id).Should().BeEquivalentTo([catalog.Id, pricing.Id]);
    }

    [Fact]
    public async Task BuildQuery_KeywordMatchesTheMetadataName()
    {
        using var ctx = SalesRepTestContext.Create();
        var named = await UploadAsync(ctx, "Doc1.pdf", "Catalogs", new SalesRepDocumentMetadata { Name = "Winter collection" });
        await UploadAsync(ctx, "Doc2.pdf", "Catalogs", new SalesRepDocumentMetadata { Name = "Summer lookbook" });

        var searchService = ctx.GetRequiredService<ISalesRepDocumentMetadataSearchService>();

        // The metadata-side keyword is a DB predicate over the metadata Name (the case-insensitive, file-name-aware
        // keyword is the orchestrator's in-memory concern).
        var result = await searchService.SearchAsync(new SalesRepDocumentMetadataSearchCriteria { Keyword = "Winter", Take = 100 });
        result.Results.Select(x => x.Id).Should().Equal(named.Id);
    }

    [Fact]
    public async Task BuildSortExpression_DefaultsToPinnedFirst()
    {
        using var ctx = SalesRepTestContext.Create();
        await UploadAsync(ctx, "First.pdf", "Catalogs");
        var pinned = await UploadAsync(ctx, "Second.pdf", "Catalogs");

        var metadataService = ctx.GetRequiredService<ISalesRepDocumentMetadataService>();
        await metadataService.SetPinnedAsync(pinned.Id, isPinned: true);

        var searchService = ctx.GetRequiredService<ISalesRepDocumentMetadataSearchService>();

        // Default sort => the pinned row leads.
        var result = await searchService.SearchAsync(new SalesRepDocumentMetadataSearchCriteria { Take = 100 });
        result.Results.First().Id.Should().Be(pinned.Id);
        result.Results.First().IsPinned.Should().BeTrue();
    }

    private static async Task<SalesRepDocument> UploadAsync(
        SalesRepTestContext ctx,
        string fileName,
        string category,
        SalesRepDocumentMetadata metadata = null)
    {
        var service = ctx.GetRequiredService<ISalesRepDocumentService>();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        return await service.UploadAsync(stream, fileName, category, metadata);
    }
}
