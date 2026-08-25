using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.AssetsModule.Core.Services;
using VirtoCommerce.FileExperienceApi.Core.Extensions;
using VirtoCommerce.FileExperienceApi.Core.Services;
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
/// The documents library against the REAL file-experience-api upload service and assets services (AssetEntry CRUD +
/// search on in-memory SQLite) and the module's own metadata DbContext — only the binary storage is an in-memory
/// double. Covers the two-step intake (file upload → library registration with ownership) → search round-trip
/// (paging, default createdDate:desc sort, keyword, category filter/counts), the delete cascade (metadata +
/// AssetEntry + blob), and scope isolation (foreign files must never appear or be deleted).
/// </summary>
[Trait("Category", "Component")]
public class SalesRepDocumentsComponentTests
{
    [Fact]
    public async Task UploadThenSearch_RoundTripsWithPagingSortKeywordAndCategories()
    {
        using var ctx = SalesRepTestContext.Create();

        var oldest = await ctx.UploadDocumentAsync("Spring Catalog.pdf", "Catalogs");
        var middle = await ctx.UploadDocumentAsync("Lookbook.pdf", "Lookbooks");
        var newest = await ctx.UploadDocumentAsync("Price List.xlsx", "Pricing", new SalesRepDocumentMetadata { Summary = "Current prices", PageCount = 3 });

        await ctx.SetDocumentCreatedDateAsync(oldest.Id, Utc(2026, 1, 1));
        await ctx.SetDocumentCreatedDateAsync(middle.Id, Utc(2026, 2, 1));
        await ctx.SetDocumentCreatedDateAsync(newest.Id, Utc(2026, 3, 1));

        var searchService = ctx.GetRequiredService<ISalesRepDocumentSearchService>();

        // Default sort = createdDate:desc.
        var all = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria());
        all.TotalCount.Should().Be(3);
        all.Results.Select(x => x.Id).Should().Equal(newest.Id, middle.Id, oldest.Id);

        // Metadata joined into search results; Url points at the file-experience-api endpoint.
        var priced = all.Results.Single(x => x.Id == newest.Id);
        priced.Summary.Should().Be("Current prices");
        priced.PageCount.Should().Be(3);
        priced.Url.Should().Be($"/api/files/{newest.FileId}");

        // Paging keeps the total while slicing.
        var firstPage = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { Take = 2 });
        firstPage.TotalCount.Should().Be(3);
        firstPage.Results.Select(x => x.Id).Should().Equal(newest.Id, middle.Id);

        var secondPage = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { Skip = 2, Take = 2 });
        secondPage.Results.Select(x => x.Id).Should().Equal(oldest.Id);

        // Keyword matches the document name. Case-insensitivity is collation-provided (per-provider DB config,
        // not exercisable on the SQLite harness), so component tests use exact casing.
        var byKeyword = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { Keyword = "Lookbook" });
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

        // The platform ISearchService conformance makes the generic extensions usable (pages Take=2 until exhausted).
        (await searchService.SearchAllAsync(new SalesRepDocumentSearchCriteria { Take = 2 })).Should().HaveCount(3);
    }

    [Fact]
    public async Task Upload_TakesOwnershipOfTheFile()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await ctx.UploadDocumentAsync("Owned.pdf", "Catalogs");

        var file = (await ctx.GetRequiredService<IFileUploadService>().GetAsync([document.FileId])).Single();
        file.Scope.Should().Be(ModuleConstants.DocumentsScope);
        file.OwnerEntityId.Should().Be(document.Id);
        file.OwnerTypeIs<SalesRepDocumentMetadata>().Should().BeTrue();

        // A second registration of the same file must be refused — the file already belongs to a document.
        var documentService = ctx.GetRequiredService<ISalesRepDocumentService>();
        var recreate = () => documentService.CreateAsync(document.FileId, "Catalogs");
        await recreate.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already belongs*");
    }

    [Fact]
    public async Task Download_StreamsTheUploadedContentThroughTheFileService()
    {
        using var ctx = SalesRepTestContext.Create();
        var uploaded = await ctx.UploadDocumentAsync("Guide.pdf", "Guides", content: "guide-content");

        var documentService = ctx.GetRequiredService<ISalesRepDocumentService>();
        var document = await documentService.GetByIdAsync(uploaded.Id);
        document.Name.Should().Be("Guide.pdf");
        document.Category.Should().Be("Guides");

        // The download endpoint (GET /api/files/{id}) streams through the file service by the document's FileId.
        await using var stream = await ctx.GetRequiredService<IFileUploadService>().OpenReadAsync(document.FileId);
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).Should().Be("guide-content");
    }

    [Fact]
    public async Task GetById_ReturnsTheDocumentWithMetadataAndNullForUnknownId()
    {
        using var ctx = SalesRepTestContext.Create();
        var uploaded = await ctx.UploadDocumentAsync("Info.pdf", "Guides", new SalesRepDocumentMetadata { Summary = "About", PageCount = 2 });

        var documentService = ctx.GetRequiredService<ISalesRepDocumentService>();

        var document = await documentService.GetByIdAsync(uploaded.Id);
        document.Name.Should().Be("Info.pdf");
        document.Category.Should().Be("Guides");
        document.Summary.Should().Be("About");
        document.PageCount.Should().Be(2);
        document.FileId.Should().Be(uploaded.FileId);
        document.Url.Should().Be($"/api/files/{uploaded.FileId}");

        (await documentService.GetByIdAsync("missing-id")).Should().BeNull();
    }

    [Fact]
    public async Task Delete_CascadesToMetadataAssetEntryAndBlob()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await ctx.UploadDocumentAsync("Doomed.pdf", "Catalogs", new SalesRepDocumentMetadata { Summary = "gone soon" });

        var blobProvider = ctx.GetRequiredService<InMemoryBlobStorageProvider>();
        blobProvider.BlobUrls.Should().HaveCount(1);

        await ctx.GetRequiredService<ISalesRepDocumentService>().DeleteAsync([document.Id]);

        // Blob gone.
        blobProvider.BlobUrls.Should().BeEmpty();

        // AssetEntry row gone.
        var entries = await ctx.GetRequiredService<IAssetEntryService>().GetAsync([document.FileId]);
        entries.Should().BeEmpty();

        // Metadata row gone.
        using var db = ctx.NewSalesRepDbContext();
        (await db.Set<DocumentMetadataEntity>().CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);

        // And the search surface agrees.
        var result = await ctx.GetRequiredService<ISalesRepDocumentSearchService>().SearchAsync(new SalesRepDocumentSearchCriteria());
        result.TotalCount.Should().Be(0);
    }

    // A storage failure mid-delete cannot keep the document: the file service deletes the record before the blob
    // and its AssetEntryChangedEvent cascade empties the metadata first, so the delete CONVERGES — the document
    // is fully gone from the database, the leaked blob is the tolerated debris, and a repeated call is a no-op.
    // (Runs over the real event bus, so the cascade genuinely fires before the blob step throws.)
    [Fact]
    public async Task Delete_BlobRemovalFails_StillRemovesTheDocument()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await ctx.UploadDocumentAsync("Doomed.pdf", "Catalogs");

        var blobProvider = ctx.GetRequiredService<InMemoryBlobStorageProvider>();
        blobProvider.FailOnRemoveWith = new IOException("Storage offline.");

        var documentService = ctx.GetRequiredService<ISalesRepDocumentService>();
        await documentService.DeleteAsync([document.Id]);

        // Metadata and file record are gone; only the blob remains as debris.
        (await documentService.GetByIdAsync(document.Id)).Should().BeNull();
        (await ctx.GetRequiredService<ISalesRepDocumentSearchService>().SearchAsync(new SalesRepDocumentSearchCriteria()))
            .TotalCount.Should().Be(0);
        blobProvider.BlobUrls.Should().HaveCount(1);

        // Idempotent: repeating the call finds nothing and succeeds.
        await documentService.DeleteAsync([document.Id]);
    }

    // Deleting the file record through any IAssetEntryService path (the generic deleteFile mutation, the platform
    // asset admin APIs) raises AssetEntryChangedEvent; the module's handler must drop the sidecar metadata row so
    // no invisible orphan inflates TotalCount.
    [Fact]
    public async Task FileRecordDeletion_CascadesToTheMetadataRow()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await ctx.UploadDocumentAsync("Doomed.pdf", "Catalogs");

        await ctx.GetRequiredService<IAssetEntryService>().DeleteAsync([document.FileId]);

        using (var db = ctx.NewSalesRepDbContext())
        {
            (await db.Set<DocumentMetadataEntity>().CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        }

        var result = await ctx.GetRequiredService<ISalesRepDocumentSearchService>().SearchAsync(new SalesRepDocumentSearchCriteria());
        result.TotalCount.Should().Be(0);
    }

    // The metadata list is authoritative for search: when the file record vanished WITHOUT the event cascade
    // (raw SQL, a mid-cascade failure), the document still lists — file-derived fields degrade to null instead
    // of the row silently dropping while TotalCount keeps counting it. The orphan stays visible and deletable.
    [Fact]
    public async Task Search_FileRecordGoneWithoutTheCascade_KeepsTheRowWithDegradedFileFields()
    {
        using var ctx = SalesRepTestContext.Create();
        var intact = await ctx.UploadDocumentAsync("Intact.pdf", "Catalogs");
        var orphaned = await ctx.UploadDocumentAsync("Orphaned.pdf", "Catalogs");

        await ctx.DeleteAssetEntryRowAsync(orphaned.FileId);

        var result = await ctx.GetRequiredService<ISalesRepDocumentSearchService>().SearchAsync(new SalesRepDocumentSearchCriteria());

        result.TotalCount.Should().Be(2);
        result.Results.Should().HaveCount(2);

        var degraded = result.Results.Single(x => x.Id == orphaned.Id);
        degraded.DisplayName.Should().Be("Orphaned.pdf");
        degraded.Name.Should().BeNull();
        degraded.ContentType.Should().BeNull();
        degraded.Size.Should().BeNull();
        // The download URL stays resolvable — attempting it yields the server's 404, uniformly with blob loss.
        degraded.Url.Should().Be($"/api/files/{orphaned.FileId}");

        result.Results.Single(x => x.Id == intact.Id).Url.Should().Be($"/api/files/{intact.FileId}");

        // The visible orphan converges away through the ordinary module delete.
        await ctx.GetRequiredService<ISalesRepDocumentService>().DeleteAsync([orphaned.Id]);
        (await ctx.GetRequiredService<ISalesRepDocumentSearchService>().SearchAsync(new SalesRepDocumentSearchCriteria())).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Delete_ToleratesAnAlreadyMissingBlob()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await ctx.UploadDocumentAsync("Vanished.pdf", "Catalogs");

        var blobProvider = ctx.GetRequiredService<InMemoryBlobStorageProvider>();
        await blobProvider.RemoveAsync([.. blobProvider.BlobUrls]);

        var documentService = ctx.GetRequiredService<ISalesRepDocumentService>();
        await documentService.DeleteAsync([document.Id]);

        (await documentService.GetByIdAsync(document.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Library_MustNotSeeFilesOfOtherScopes()
    {
        using var ctx = SalesRepTestContext.Create();

        var library = await ctx.UploadDocumentAsync("Mine.pdf", "Catalogs");
        var foreignId = await SeedForeignAssetEntryAsync(ctx, group: "product-images");
        var groupLessId = await SeedForeignAssetEntryAsync(ctx, group: null);

        // Isolation: files of a different (or absent) scope must NOT appear anywhere.
        var searchService = ctx.GetRequiredService<ISalesRepDocumentSearchService>();
        var result = await searchService.SearchAsync(new SalesRepDocumentSearchCriteria());
        result.Results.Select(x => x.Id).Should().Equal(library.Id);

        var categories = await searchService.GetCategoriesAsync();
        categories.Select(x => x.Name).Should().Equal("Catalogs");

        var documentService = ctx.GetRequiredService<ISalesRepDocumentService>();
        (await documentService.GetByIdAsync(foreignId)).Should().BeNull();

        // A foreign file cannot be registered as a library document.
        var create = () => documentService.CreateAsync(foreignId, "Catalogs");
        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");

        // Delete must refuse to touch foreign entries even when handed their ids.
        await documentService.DeleteAsync([foreignId, groupLessId]);
        var foreignEntries = await ctx.GetRequiredService<IAssetEntryService>().GetAsync([foreignId, groupLessId]);
        foreignEntries.Should().HaveCount(2);
    }

    [Fact]
    public async Task MetadataSave_UpdatesAnExistingDocumentInPlace()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await ctx.UploadDocumentAsync("Editable.pdf", "Guides", new SalesRepDocumentMetadata { Summary = "v1" });

        // Full-replace semantics: the save must carry every field that should survive (category included).
        var metadataService = ctx.GetRequiredService<ISalesRepDocumentMetadataService>();
        await metadataService.SaveChangesAsync([new SalesRepDocumentMetadata { Id = document.Id, FileId = document.FileId, Name = "Pretty name", Category = "Manuals", Summary = "v2", PageCount = 7 }]);

        var reloaded = await ctx.GetRequiredService<ISalesRepDocumentService>().GetByIdAsync(document.Id);
        reloaded.Name.Should().Be("Editable.pdf");
        reloaded.DisplayName.Should().Be("Pretty name");
        reloaded.Category.Should().Be("Manuals");
        reloaded.Summary.Should().Be("v2");
        reloaded.PageCount.Should().Be(7);

        using var db = ctx.NewSalesRepDbContext();
        (await db.Set<DocumentMetadataEntity>().CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task MetadataSave_RejectsAnOverlongCategory()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await ctx.UploadDocumentAsync("Editable.pdf", "Guides");

        var metadataService = ctx.GetRequiredService<ISalesRepDocumentMetadataService>();
        var save = () => metadataService.SaveChangesAsync([new SalesRepDocumentMetadata
        {
            Id = document.Id,
            FileId = document.FileId,
            Name = "Editable.pdf",
            Category = new string('x', ModuleConstants.Documents.CategoryMaxLength + 1),
        }]);

        await save.Should().ThrowAsync<ValidationException>().WithMessage("*32*");
        (await ctx.GetRequiredService<ISalesRepDocumentService>().GetByIdAsync(document.Id)).Category.Should().Be("Guides");
    }

    // The category is mandatory on EVERY save — a full-replace metadata PUT that omits it must be rejected
    // instead of silently clearing the field (which would drop the document out of every category listing).
    [Fact]
    public async Task MetadataSave_RejectsAMissingCategory()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await ctx.UploadDocumentAsync("Editable.pdf", "Guides");

        var controller = CreateController(ctx);
        var response = (await controller.UpdateMetadata(document.Id, new SalesRepDocumentMetadata { Summary = "no category" }))
            .Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        response.Value.ToString().Should().Contain("Category");

        (await ctx.GetRequiredService<ISalesRepDocumentService>().GetByIdAsync(document.Id)).Category.Should().Be("Guides");
    }

    [Fact]
    public async Task Create_TrimsTheCategory()
    {
        using var ctx = SalesRepTestContext.Create();

        var document = await ctx.UploadDocumentAsync("Padded.pdf", " Catalogs ");

        document.Category.Should().Be("Catalogs");
        (await ctx.GetRequiredService<ISalesRepDocumentService>().GetByIdAsync(document.Id)).Category.Should().Be("Catalogs");
    }

    // A removed/unknown sort token (e.g. the retired "size") falls back to the default ordering instead of throwing.
    [Fact]
    public async Task Search_UnknownSortToken_FallsBackToTheDefaultOrder()
    {
        using var ctx = SalesRepTestContext.Create();
        var older = await ctx.UploadDocumentAsync("Older.pdf", "Catalogs");
        var newer = await ctx.UploadDocumentAsync("Newer.pdf", "Catalogs");
        await ctx.SetDocumentCreatedDateAsync(older.Id, Utc(2026, 1, 1));
        await ctx.SetDocumentCreatedDateAsync(newer.Id, Utc(2026, 3, 1));

        var result = await ctx.GetRequiredService<ISalesRepDocumentSearchService>()
            .SearchAsync(new SalesRepDocumentSearchCriteria { Sort = "size:desc" });

        result.Results.Select(x => x.Id).Should().Equal(newer.Id, older.Id);
    }

    // The "name" sort orders by the DISPLAY name (metadata override), not the raw file name.
    [Fact]
    public async Task Search_NameSort_UsesTheDisplayName()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.UploadDocumentAsync("AAA.pdf", "Catalogs", new SalesRepDocumentMetadata { Name = "Yellow pages" });
        await ctx.UploadDocumentAsync("BBB.pdf", "Catalogs");

        var result = await ctx.GetRequiredService<ISalesRepDocumentSearchService>()
            .SearchAsync(new SalesRepDocumentSearchCriteria { Sort = "name:asc" });

        // A file-name sort would put AAA.pdf first.
        result.Results.Select(x => x.DisplayName).Should().Equal("BBB.pdf", "Yellow pages");
    }

    [Fact]
    public async Task Pin_PinningOneDocumentUnpinsEveryOther()
    {
        using var ctx = SalesRepTestContext.Create();
        var first = await ctx.UploadDocumentAsync("First.pdf", "Catalogs");
        var second = await ctx.UploadDocumentAsync("Second.pdf", "Catalogs");

        var controller = CreateController(ctx);
        var searchService = ctx.GetRequiredService<ISalesRepDocumentSearchService>();

        (await controller.Pin(first.Id)).Should().BeOfType<NoContentResult>();

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
        (await controller.Unpin(second.Id)).Should().BeOfType<NoContentResult>();
        (await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { IsPinned = true })).TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task PinUnpinAndMetadata_UnknownId_Return404()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.UploadDocumentAsync("Only.pdf", "Catalogs");

        var controller = CreateController(ctx);

        (await controller.Pin("no-such-id")).Should().BeOfType<NotFoundResult>();
        (await controller.Unpin("no-such-id")).Should().BeOfType<NotFoundResult>();

        // A metadata PUT to an unknown id must not create an orphan row — it is a not-found.
        (await controller.UpdateMetadata("no-such-id", new SalesRepDocumentMetadata { Category = "Catalogs" }))
            .Result.Should().BeOfType<NotFoundResult>();
        using (var db = ctx.NewSalesRepDbContext())
        {
            (await db.Set<DocumentMetadataEntity>().CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        }

        // A foreign AssetEntry id is not a library document either.
        var foreignId = await SeedForeignAssetEntryAsync(ctx, group: "product-images");
        (await controller.Pin(foreignId)).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task MetadataPut_PreservesThePinStateAndIgnoresIncomingIsPinned()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await ctx.UploadDocumentAsync("Pinnable.pdf", "Catalogs", new SalesRepDocumentMetadata { Summary = "v1" });

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

    // The pin column has exactly one writer — SetPinnedAsync's atomic set-based UPDATE. IsPinned is absent from
    // the entity's FromModel AND Patch, so the published ICrudService surface can neither change an existing
    // row's pin nor insert a pre-pinned row: the invariant holds structurally, with no lock and no concurrency
    // harness needed.
    [Fact]
    public async Task MetadataSave_CannotTouchThePinColumn()
    {
        using var ctx = SalesRepTestContext.Create();
        var pinned = await ctx.UploadDocumentAsync("Pinned.pdf", "Catalogs");
        var other = await ctx.UploadDocumentAsync("Other.pdf", "Catalogs");

        var metadataService = ctx.GetRequiredService<ISalesRepDocumentMetadataService>();
        (await metadataService.SetPinnedAsync(pinned.Id, isPinned: true)).Should().BeTrue();

        // A downstream consumer saves contradictory pin flags through the inherited SaveChangesAsync…
        var models = await metadataService.GetAsync([pinned.Id, other.Id]);
        models.Single(x => x.Id == pinned.Id).IsPinned = false;
        models.Single(x => x.Id == other.Id).IsPinned = true;
        await metadataService.SaveChangesAsync(models);

        // …and inserts a brand-new row already carrying IsPinned = true.
        var prePinned = new SalesRepDocumentMetadata { FileId = Guid.NewGuid().ToString("N"), Name = "Sneaky.pdf", Category = "Catalogs", IsPinned = true };
        await metadataService.SaveChangesAsync([prePinned]);

        var pinnedRows = await ctx.GetRequiredService<ISalesRepDocumentSearchService>()
            .SearchAsync(new SalesRepDocumentSearchCriteria { IsPinned = true });
        pinnedRows.Results.Single().Id.Should().Be(pinned.Id);
    }

    // The PUT response must carry the STORED audit stamps, which the request body does not have: the created
    // date is written raw into the row and can only reach the response through the post-save re-read. Noon +
    // .Date comparison keeps the assertion immune to the harness's local→UTC read shift (CI runs on UTC,
    // dev boxes usually don't — see the vc-datetime-utc rule).
    [Fact]
    public async Task MetadataPut_ResponseCarriesTheStoredAuditFields()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await ctx.UploadDocumentAsync("Audited.pdf", "Catalogs");
        await ctx.SetDocumentCreatedDateAsync(document.Id, new System.DateTime(2026, 3, 1, 12, 0, 0, System.DateTimeKind.Utc));

        var updated = (await CreateController(ctx).UpdateMetadata(document.Id, new SalesRepDocumentMetadata { Category = "Catalogs", Name = "Renamed" }))
            .Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<SalesRepDocument>().Subject;

        updated.CreatedDate.Date.Should().Be(new System.DateTime(2026, 3, 1));
        updated.ModifiedDate.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCategories_ComputesCountsOverTheKeywordFilteredSet()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.UploadDocumentAsync("Spring Catalog.pdf", "Catalogs");
        await ctx.UploadDocumentAsync("Fall Catalog.pdf", "Catalogs");
        await ctx.UploadDocumentAsync("Lookbook.pdf", "Lookbooks");

        var searchService = ctx.GetRequiredService<ISalesRepDocumentSearchService>();

        var all = await searchService.GetCategoriesAsync();
        all.Select(x => (x.Name, x.Count)).Should().Equal(("Catalogs", 2), ("Lookbooks", 1));

        // Keyword narrows the counted set; zero-count categories are omitted.
        var fallOnly = await searchService.GetCategoriesAsync("Fall");
        fallOnly.Select(x => (x.Name, x.Count)).Should().Equal(("Catalogs", 1));

        (await searchService.GetCategoriesAsync("no-such-document")).Should().BeEmpty();

        // The keyword also matches display names.
        var metadataService = ctx.GetRequiredService<ISalesRepDocumentMetadataService>();
        var lookbook = (await searchService.SearchAsync(new SalesRepDocumentSearchCriteria { Category = "Lookbooks" })).Results.Single();
        await metadataService.SaveChangesAsync([new SalesRepDocumentMetadata { Id = lookbook.Id, FileId = lookbook.FileId, Category = "Lookbooks", Name = "Winter collection" }]);

        var byDisplayName = await searchService.GetCategoriesAsync("Winter");
        byDisplayName.Select(x => (x.Name, x.Count)).Should().Equal(("Lookbooks", 1));
    }

    private static SalesRepDocumentsController CreateController(SalesRepTestContext ctx)
        => new(
            ctx.GetRequiredService<ISalesRepDocumentService>(),
            ctx.GetRequiredService<ISalesRepDocumentSearchService>(),
            ctx.GetRequiredService<ISalesRepDocumentMetadataService>());

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
