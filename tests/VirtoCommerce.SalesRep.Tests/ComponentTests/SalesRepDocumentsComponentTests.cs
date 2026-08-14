using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.AssetsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Models;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Web.Controllers.Api;
using Xunit;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// The documents library against the REAL assets services (AssetEntry CRUD + search on in-memory SQLite) and the
/// module's own metadata DbContext — only the binary storage is an in-memory double. Covers the upload→search
/// round-trip (paging, default createdDate:desc sort, keyword, category filter/counts), the delete cascade
/// (metadata + AssetEntry + blob), and Group isolation (foreign AssetEntries must never appear or be deleted).
/// </summary>
[Trait("Category", "Component")]
public class SalesRepDocumentsComponentTests
{
    [Fact]
    public async Task UploadThenSearch_RoundTripsWithPagingSortKeywordAndCategories()
    {
        using var ctx = SalesRepTestContext.Create();

        var oldest = await UploadAsync(ctx, "Spring Catalog.pdf", "Catalogs");
        var middle = await UploadAsync(ctx, "Lookbook.pdf", "Lookbooks");
        var newest = await UploadAsync(ctx, "Price List.xlsx", "Pricing", new SalesRepDocumentMetadata { Summary = "Current prices", PageCount = 3 });

        await ctx.SetDocumentCreatedDateAsync(oldest.Id, Utc(2026, 1, 1));
        await ctx.SetDocumentCreatedDateAsync(middle.Id, Utc(2026, 2, 1));
        await ctx.SetDocumentCreatedDateAsync(newest.Id, Utc(2026, 3, 1));

        var searchService = ctx.GetRequiredService<ISalesRepDocumentSearchService>();

        // Default sort = createdDate:desc.
        var all = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria());
        all.TotalCount.Should().Be(3);
        all.Results.Select(x => x.Id).Should().Equal(newest.Id, middle.Id, oldest.Id);

        // Metadata joined into search results; Url points at the module's endpoint.
        var priced = all.Results.Single(x => x.Id == newest.Id);
        priced.Summary.Should().Be("Current prices");
        priced.PageCount.Should().Be(3);
        priced.Url.Should().Be($"/api/sales-rep/documents/{newest.Id}");

        // Paging keeps the total while slicing.
        var firstPage = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { Take = 2 });
        firstPage.TotalCount.Should().Be(3);
        firstPage.Results.Select(x => x.Id).Should().Equal(newest.Id, middle.Id);

        var secondPage = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { Skip = 2, Take = 2 });
        secondPage.Results.Select(x => x.Id).Should().Equal(oldest.Id);

        // Keyword matches the document name, case-insensitively.
        var byKeyword = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { Keyword = "lookbook" });
        byKeyword.Results.Select(x => x.Id).Should().Equal(middle.Id);

        // Category filter = the first-level subfolder.
        var byCategory = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { Category = "Catalogs" });
        byCategory.Results.Select(x => x.Id).Should().Equal(oldest.Id);

        // ObjectIds pins the result to the requested documents (sort still applies).
        var byIds = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { ObjectIds = [oldest.Id, newest.Id] });
        byIds.TotalCount.Should().Be(2);
        byIds.Results.Select(x => x.Id).Should().Equal(newest.Id, oldest.Id);

        // Explicit name sort.
        var byName = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { Sort = "name:asc" });
        byName.Results.Select(x => x.Name).Should().Equal("Lookbook.pdf", "Price List.xlsx", "Spring Catalog.pdf");

        var categories = await searchService.GetCategoriesAsync();
        categories.Select(x => (x.Name, x.Count)).Should().Equal(("Catalogs", 1), ("Lookbooks", 1), ("Pricing", 1));
    }

    [Fact]
    public async Task Download_StreamsTheUploadedContent()
    {
        using var ctx = SalesRepTestContext.Create();
        var uploaded = await UploadAsync(ctx, "Guide.pdf", "Guides", content: "guide-content");

        var documentService = ctx.GetRequiredService<ISalesRepDocumentService>();
        var document = await documentService.GetAsync(uploaded.Id);
        document.Name.Should().Be("Guide.pdf");
        document.Category.Should().Be("Guides");

        await using var stream = await documentService.OpenReadAsync(uploaded.Id);
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync()).Should().Be("guide-content");
    }

    [Fact]
    public async Task GetById_ReturnsTheDocumentWithMetadataAndNullForUnknownId()
    {
        using var ctx = SalesRepTestContext.Create();
        var uploaded = await UploadAsync(ctx, "Info.pdf", "Guides", new SalesRepDocumentMetadata { Summary = "About", PageCount = 2 });

        var documentService = ctx.GetRequiredService<ISalesRepDocumentService>();

        var document = await documentService.GetAsync(uploaded.Id);
        document.Name.Should().Be("Info.pdf");
        document.Category.Should().Be("Guides");
        document.Summary.Should().Be("About");
        document.PageCount.Should().Be(2);
        document.Url.Should().Be($"/api/sales-rep/documents/{uploaded.Id}");

        (await documentService.GetAsync("missing-id")).Should().BeNull();
    }

    [Fact]
    public async Task Delete_CascadesToMetadataAssetEntryAndBlob()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await UploadAsync(ctx, "Doomed.pdf", "Catalogs", new SalesRepDocumentMetadata { Summary = "gone soon" });

        var blobProvider = ctx.GetRequiredService<InMemoryBlobStorageProvider>();
        blobProvider.BlobUrls.Should().HaveCount(1);

        await ctx.GetRequiredService<ISalesRepDocumentService>().DeleteAsync([document.Id]);

        // Blob gone.
        blobProvider.BlobUrls.Should().BeEmpty();

        // AssetEntry row gone.
        var entries = await ctx.GetRequiredService<IAssetEntryService>().GetAsync([document.Id]);
        entries.Should().BeEmpty();

        // Metadata row gone.
        using var db = ctx.NewSalesRepDbContext();
        (await db.Set<DocumentMetadataEntity>().CountAsync()).Should().Be(0);

        // And the search surface agrees.
        var result = await ctx.GetRequiredService<ISalesRepDocumentSearchService>().SearchAsync(new SalesRepDocumentSearchCriteria());
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_ToleratesAnAlreadyMissingBlob()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await UploadAsync(ctx, "Vanished.pdf", "Catalogs");

        var blobProvider = ctx.GetRequiredService<InMemoryBlobStorageProvider>();
        await blobProvider.RemoveAsync([.. blobProvider.BlobUrls]);

        var documentService = ctx.GetRequiredService<ISalesRepDocumentService>();
        await documentService.DeleteAsync([document.Id]);

        (await documentService.GetAsync(document.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Search_MustNotSeeAssetEntriesOfOtherGroups()
    {
        using var ctx = SalesRepTestContext.Create();

        var library = await UploadAsync(ctx, "Mine.pdf", "Catalogs");
        var foreignId = await SeedForeignAssetEntryAsync(ctx, group: "product-images");
        var groupLessId = await SeedForeignAssetEntryAsync(ctx, group: null);

        var searchService = ctx.GetRequiredService<ISalesRepDocumentSearchService>();

        // Isolation: entries of a different (or absent) Group must NOT appear anywhere.
        var result = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria());
        result.Results.Select(x => x.Id).Should().Equal(library.Id);
        result.Results.Select(x => x.Id).Should().NotContain([foreignId, groupLessId]);

        var categories = await searchService.GetCategoriesAsync();
        categories.Select(x => x.Name).Should().Equal("Catalogs");

        var documentService = ctx.GetRequiredService<ISalesRepDocumentService>();
        (await documentService.GetAsync(foreignId)).Should().BeNull();
        (await documentService.OpenReadAsync(foreignId)).Should().BeNull();

        // Delete must refuse to touch foreign entries even when handed their ids.
        await documentService.DeleteAsync([foreignId, groupLessId]);
        var foreignEntries = await ctx.GetRequiredService<IAssetEntryService>().GetAsync([foreignId, groupLessId]);
        foreignEntries.Should().HaveCount(2);
    }

    [Fact]
    public async Task MetadataSave_UpdatesAnExistingDocumentInPlace()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await UploadAsync(ctx, "Editable.pdf", "Guides", new SalesRepDocumentMetadata { Summary = "v1" });

        // Full-replace semantics: the save must carry every field that should survive (category included).
        var metadataService = ctx.GetRequiredService<ISalesRepDocumentMetadataService>();
        await metadataService.SaveAsync([new SalesRepDocumentMetadata { Id = document.Id, Name = "Pretty name", Category = "Manuals", Summary = "v2", PageCount = 7 }]);

        var reloaded = await ctx.GetRequiredService<ISalesRepDocumentService>().GetAsync(document.Id);
        reloaded.Name.Should().Be("Editable.pdf");
        reloaded.DisplayName.Should().Be("Pretty name");
        reloaded.Category.Should().Be("Manuals");
        reloaded.Summary.Should().Be("v2");
        reloaded.PageCount.Should().Be(7);

        using var db = ctx.NewSalesRepDbContext();
        (await db.Set<DocumentMetadataEntity>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task MetadataSave_RejectsAnOverlongCategory()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await UploadAsync(ctx, "Editable.pdf", "Guides");

        var metadataService = ctx.GetRequiredService<ISalesRepDocumentMetadataService>();
        var save = () => metadataService.SaveAsync([new SalesRepDocumentMetadata
        {
            Id = document.Id,
            Category = new string('x', ModuleConstants.Documents.CategoryMaxLength + 1),
        }]);

        await save.Should().ThrowAsync<ArgumentException>().WithMessage("*32*");
        (await ctx.GetRequiredService<ISalesRepDocumentService>().GetAsync(document.Id)).Category.Should().Be("Guides");
    }

    [Fact]
    public async Task Pin_PinningOneDocumentUnpinsEveryOther()
    {
        using var ctx = SalesRepTestContext.Create();
        var first = await UploadAsync(ctx, "First.pdf", "Catalogs");
        var second = await UploadAsync(ctx, "Second.pdf", "Catalogs");

        var controller = CreateController(ctx);
        var searchService = ctx.GetRequiredService<ISalesRepDocumentSearchService>();

        var pinnedFirst = (await controller.Pin(first.Id)).Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<SalesRepDocument>().Subject;
        pinnedFirst.IsPinned.Should().BeTrue();

        var afterFirstPin = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria());
        afterFirstPin.Results.Where(x => x.IsPinned).Select(x => x.Id).Should().Equal(first.Id);

        // Pinning the second must clear the first — a single pinned document at most.
        await controller.Pin(second.Id);

        var afterSecondPin = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria());
        afterSecondPin.Results.Where(x => x.IsPinned).Select(x => x.Id).Should().Equal(second.Id);

        var pinnedOnly = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { IsPinned = true });
        pinnedOnly.TotalCount.Should().Be(1);
        pinnedOnly.Results.Single().Id.Should().Be(second.Id);

        // Unpinning is plain — no document stays pinned.
        var unpinned = (await controller.Unpin(second.Id)).Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<SalesRepDocument>().Subject;
        unpinned.IsPinned.Should().BeFalse();
        (await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { IsPinned = true })).TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task PinAndUnpin_UnknownId_Return404()
    {
        using var ctx = SalesRepTestContext.Create();
        await UploadAsync(ctx, "Only.pdf", "Catalogs");

        var controller = CreateController(ctx);

        (await controller.Pin("no-such-id")).Result.Should().BeOfType<NotFoundResult>();
        (await controller.Unpin("no-such-id")).Result.Should().BeOfType<NotFoundResult>();

        // A foreign AssetEntry id is not a library document either.
        var foreignId = await SeedForeignAssetEntryAsync(ctx, group: "product-images");
        (await controller.Pin(foreignId)).Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task MetadataPut_PreservesThePinStateAndIgnoresIncomingIsPinned()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await UploadAsync(ctx, "Pinnable.pdf", "Catalogs", new SalesRepDocumentMetadata { Summary = "v1" });

        var controller = CreateController(ctx);
        await controller.Pin(document.Id);

        // A full-replace metadata PUT carrying IsPinned=false must still leave the document pinned.
        var updated = (await controller.UpdateMetadata(document.Id, new SalesRepDocumentMetadata { Name = "Renamed", Category = "Manuals", Summary = "v2", IsPinned = false }))
            .Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<SalesRepDocument>().Subject;
        updated.IsPinned.Should().BeTrue();
        updated.DisplayName.Should().Be("Renamed");
        updated.Category.Should().Be("Manuals");
        updated.Summary.Should().Be("v2");

        // And the PUT cannot pin either: IsPinned=true on an unpinned document is ignored.
        await controller.Unpin(document.Id);
        var stillUnpinned = (await controller.UpdateMetadata(document.Id, new SalesRepDocumentMetadata { Category = "Manuals", IsPinned = true }))
            .Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<SalesRepDocument>().Subject;
        stillUnpinned.IsPinned.Should().BeFalse();

        var searchService = ctx.GetRequiredService<ISalesRepDocumentSearchService>();
        (await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { IsPinned = true })).TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetCategories_ComputesCountsOverTheKeywordFilteredSet()
    {
        using var ctx = SalesRepTestContext.Create();
        await UploadAsync(ctx, "Spring Catalog.pdf", "Catalogs");
        await UploadAsync(ctx, "Fall Catalog.pdf", "Catalogs");
        await UploadAsync(ctx, "Lookbook.pdf", "Lookbooks");

        var searchService = ctx.GetRequiredService<ISalesRepDocumentSearchService>();

        var all = await searchService.GetCategoriesAsync();
        all.Select(x => (x.Name, x.Count)).Should().Equal(("Catalogs", 2), ("Lookbooks", 1));

        // Keyword narrows the counted set; zero-count categories are omitted.
        var fallOnly = await searchService.GetCategoriesAsync("fall");
        fallOnly.Select(x => (x.Name, x.Count)).Should().Equal(("Catalogs", 1));

        (await searchService.GetCategoriesAsync("no-such-document")).Should().BeEmpty();

        // The keyword also matches display names.
        var metadataService = ctx.GetRequiredService<ISalesRepDocumentMetadataService>();
        var lookbook = (await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { Category = "Lookbooks" })).Results.Single();
        await metadataService.SaveAsync([new SalesRepDocumentMetadata { Id = lookbook.Id, Category = "Lookbooks", Name = "Winter collection" }]);

        var byDisplayName = await searchService.GetCategoriesAsync("winter");
        byDisplayName.Select(x => (x.Name, x.Count)).Should().Equal(("Lookbooks", 1));
    }

    // The pin/metadata endpoints never call IAuthorizationService (they are gated declaratively), so a
    // never-invoked stub satisfies the constructor.
    private static SalesRepDocumentsController CreateController(SalesRepTestContext ctx)
        => new(
            ctx.GetRequiredService<ISalesRepDocumentService>(),
            ctx.GetRequiredService<ISalesRepDocumentSearchService>(),
            ctx.GetRequiredService<ISalesRepDocumentMetadataService>(),
            new AllowAllAuthorizationService());

    private sealed class AllowAllAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }

    private static async Task<SalesRepDocument> UploadAsync(
        SalesRepTestContext ctx,
        string fileName,
        string category,
        SalesRepDocumentMetadata metadata = null,
        string content = "content")
    {
        var service = ctx.GetRequiredService<ISalesRepDocumentService>();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return await service.UploadAsync(stream, fileName, category, metadata);
    }

    private static async Task<string> SeedForeignAssetEntryAsync(SalesRepTestContext ctx, string group)
    {
        var entry = AbstractTypeFactory<AssetEntry>.TryCreateInstance();
        entry.Id = Guid.NewGuid().ToString("N");
        entry.Group = group;
        entry.BlobInfo = AbstractTypeFactory<BlobInfo>.TryCreateInstance();
        entry.BlobInfo.Name = "foreign.png";
        entry.BlobInfo.RelativeUrl = $"other-folder/foreign-{entry.Id}.png";

        await ctx.GetRequiredService<IAssetEntryService>().SaveChangesAsync([entry]);
        return entry.Id;
    }

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
