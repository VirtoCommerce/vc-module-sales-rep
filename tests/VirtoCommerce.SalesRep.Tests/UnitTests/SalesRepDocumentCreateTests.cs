using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.FileExperienceApi.Core.Extensions;
using VirtoCommerce.FileExperienceApi.Core.Models;
using VirtoCommerce.FileExperienceApi.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;
using Xunit;
using File = VirtoCommerce.FileExperienceApi.Core.Models.File;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The two-step registration flow of <see cref="SalesRepDocumentService"/> in isolation (dictionary file/metadata
/// doubles): the claimed file must exist in the library scope and be ownerless, category sanitization
/// (path traversal), ownership assignment, metadata rollback on a failed claim, and the library-scoped
/// get/update/delete surface. File content validation (extension allow-list, size limit) belongs to the
/// file-experience-api upload step and is not re-tested here.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepDocumentCreateTests
{
    private readonly FakeFileUploadService _fileUploadService = new();
    private readonly FakeMetadataService _metadataService = new();

    [Fact]
    public async Task Create_MissingFile_ThrowsAndSavesNothing()
    {
        var service = CreateService();

        var create = () => service.CreateAsync("missing-file", "Catalogs");

        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*sales-rep-documents*");
        _metadataService.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_FileFromAnotherScope_ThrowsAndSavesNothing()
    {
        var file = AddFile(scope: "quote-attachments");
        var service = CreateService();

        var create = () => service.CreateAsync(file.Id, "Catalogs");

        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*sales-rep-documents*");
        _metadataService.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_EmptyFile_ThrowsAndSavesNothing()
    {
        var file = AddFile(size: 0);
        var service = CreateService();

        var create = () => service.CreateAsync(file.Id, "Catalogs");

        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*empty*");
        _metadataService.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_AlreadyOwnedFile_ThrowsAndSavesNothing()
    {
        var file = AddFile();
        file.SetOwner<SalesRepDocumentMetadata>("other-document");
        var service = CreateService();

        var create = () => service.CreateAsync(file.Id, "Catalogs");

        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already belongs*");
        _metadataService.Saved.Should().BeEmpty();
    }

    // Category validation itself lives in the metadata service save pipeline (SalesRepDocumentMetadataValidator,
    // covered by its own tests plus the component tests); here it matters that a failed save claims nothing.
    [Fact]
    public async Task Create_MetadataSaveFails_TakesNoOwnership()
    {
        var file = AddFile();
        _metadataService.FailOnSave = true;
        var service = CreateService();

        var create = () => service.CreateAsync(file.Id, "Catalogs");

        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*metadata save failed*");
        _metadataService.Saved.Should().BeEmpty();
        file.OwnerEntityId.Should().BeNull();
    }

    [Fact]
    public async Task Create_Succeeds_MapsModelSavesMetadataAndTakesOwnership()
    {
        var file = AddFile(name: "catalog.pdf", contentType: "application/pdf", size: 7);
        var service = CreateService();
        var metadata = new SalesRepDocumentMetadata { Summary = "Spring catalog", PageCount = 42, PreviewUrl = "https://cdn/preview.png" };

        var document = await service.CreateAsync(file.Id, "Catalogs", metadata);

        var saved = _metadataService.Saved.Should().ContainSingle().Subject;
        saved.Id.Should().NotBeNullOrEmpty("the metadata row gets its own generated id");
        saved.FileId.Should().Be(file.Id);
        saved.Name.Should().Be("catalog.pdf", "the display name is always stored, defaulted from the file name");
        saved.Category.Should().Be("Catalogs");

        document.Id.Should().Be(saved.Id);
        document.FileId.Should().Be(file.Id);
        document.Name.Should().Be("catalog.pdf");
        document.DisplayName.Should().Be("catalog.pdf"); // no metadata name → falls back to the file name
        document.Category.Should().Be("Catalogs");
        document.IsPinned.Should().BeFalse();
        document.ContentType.Should().Be("application/pdf");
        document.Size.Should().Be(7);
        document.Url.Should().Be($"/api/files/{file.Id}");
        document.Summary.Should().Be("Spring catalog");
        document.PageCount.Should().Be(42);
        document.PreviewUrl.Should().Be("https://cdn/preview.png");

        file.OwnerEntityId.Should().Be(saved.Id);
        file.OwnerTypeIs<SalesRepDocumentMetadata>().Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithMetadataName_UsesItAsTheDisplayName()
    {
        var file = AddFile(name: "list.pdf");
        var service = CreateService();

        var document = await service.CreateAsync(file.Id, "Catalogs", new SalesRepDocumentMetadata { Name = "Spring price list" });

        document.Name.Should().Be("list.pdf");
        document.DisplayName.Should().Be("Spring price list");
    }

    [Fact]
    public async Task Create_FileSaveFails_RollsBackMetadata()
    {
        var file = AddFile();
        _fileUploadService.FailOnSave = true;
        var service = CreateService();

        var create = () => service.CreateAsync(file.Id, "Catalogs");

        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*file save failed*");
        _metadataService.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_MissingMetadataOrFile_ReturnsNull()
    {
        var service = CreateService();
        (await service.GetByIdAsync("missing")).Should().BeNull();

        // Metadata whose file has vanished from the store must not surface as a document.
        _metadataService.Saved.Add(new SalesRepDocumentMetadata { Id = "orphan", FileId = "gone", Category = "Catalogs" });
        (await service.GetByIdAsync("orphan")).Should().BeNull();
    }

    [Fact]
    public async Task UpdateMetadata_KeepsTheFileLinkAndPinState()
    {
        var file = AddFile(name: "list.pdf");
        var service = CreateService();
        var document = await service.CreateAsync(file.Id, "Catalogs");
        _metadataService.Saved.Single().IsPinned = true;

        var updated = await service.UpdateMetadataAsync(document.Id, new SalesRepDocumentMetadata { Name = "Renamed", Category = "Price lists" });

        updated.DisplayName.Should().Be("Renamed");
        updated.FileId.Should().Be(file.Id);
        updated.IsPinned.Should().BeTrue("a full-replace metadata PUT must not change the pin state");

        // An omitted display name resets to the file name — the display name is always stored.
        var reset = await service.UpdateMetadataAsync(document.Id, new SalesRepDocumentMetadata { Category = "Price lists" });
        reset.DisplayName.Should().Be("list.pdf");
    }

    [Fact]
    public async Task SaveChanges_CreatesNewAndUpdatesExistingDocuments()
    {
        var file = AddFile(name: "list.pdf");
        var service = CreateService();

        // No id → the model is registered as a new document (file claim included) and gets its id back.
        var created = new SalesRepDocument { FileId = file.Id, Category = "Catalogs", Summary = "v1" };
        await service.SaveChangesAsync([created]);

        created.Id.Should().NotBeNullOrEmpty();
        file.OwnerEntityId.Should().Be(created.Id);

        // With an id → full-replace metadata update (file link and pin state preserved by UpdateMetadataAsync).
        var updated = new SalesRepDocument { Id = created.Id, Name = "list.pdf", DisplayName = "Pretty", Category = "Manuals", Summary = "v2" };
        await service.SaveChangesAsync([updated]);

        var reloaded = await service.GetByIdAsync(created.Id);
        reloaded.DisplayName.Should().Be("Pretty");
        reloaded.Category.Should().Be("Manuals");
        reloaded.Summary.Should().Be("v2");
        reloaded.FileId.Should().Be(file.Id);
    }

    [Fact]
    public async Task Delete_RemovesMetadataAndFiles()
    {
        var file = AddFile();
        var service = CreateService();
        var document = await service.CreateAsync(file.Id, "Catalogs");

        await service.DeleteAsync([document.Id]);

        _metadataService.Saved.Should().BeEmpty();
        _fileUploadService.Files.Should().BeEmpty();
    }

    // Files are deleted first so a file-store failure leaves the document listed and the delete retryable —
    // the reverse order would leave a readable file unreachable through the module.
    [Fact]
    public async Task Delete_FileDeleteFails_KeepsTheDocument()
    {
        var file = AddFile();
        var service = CreateService();
        var document = await service.CreateAsync(file.Id, "Catalogs");
        _fileUploadService.FailOnDeleteWith = new InvalidOperationException("File delete failed.");

        var delete = () => service.DeleteAsync([document.Id]);

        await delete.Should().ThrowAsync<InvalidOperationException>().WithMessage("*file delete failed*");
        _metadataService.Saved.Should().ContainSingle();
    }

    // A blob already missing from the physical storage (removed directly from disk or the blob container)
    // must not block the delete — removing the file was the goal anyway.
    [Fact]
    public async Task Delete_FileMissingFromStorage_DeletesTheDocument()
    {
        var file = AddFile();
        var service = CreateService();
        var document = await service.CreateAsync(file.Id, "Catalogs");
        _fileUploadService.FailOnDeleteWith = new FileNotFoundException("Could not find file.");

        await service.DeleteAsync([document.Id]);

        _metadataService.Saved.Should().BeEmpty();
    }

    private SalesRepDocumentService CreateService() => new(_fileUploadService, _metadataService, new SalesRepMapper(), NullLogger<SalesRepDocumentService>.Instance);

    private File AddFile(string scope = ModuleConstants.DocumentsScope, string name = "list.pdf", string contentType = "application/pdf", long size = 1)
    {
        var file = new File
        {
            Id = Guid.NewGuid().ToString("N"),
            Scope = scope,
            Name = name,
            ContentType = contentType,
            Size = size,
        };
        file.PublicUrl = $"/api/files/{file.Id}";
        _fileUploadService.Files[file.Id] = file;
        return file;
    }

    private sealed class FakeFileUploadService : IFileUploadService
    {
        public Dictionary<string, File> Files { get; } = [];
        public bool FailOnSave { get; set; }
        public Exception FailOnDeleteWith { get; set; }

        public Task<IList<File>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
            => Task.FromResult<IList<File>>([.. ids.Where(Files.ContainsKey).Select(id => Files[id])]);

        public Task SaveChangesAsync(IList<File> models)
        {
            if (FailOnSave)
            {
                throw new InvalidOperationException("File save failed.");
            }

            foreach (var model in models)
            {
                Files[model.Id] = model;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(IList<string> ids, bool softDelete = false)
        {
            if (FailOnDeleteWith != null)
            {
                throw FailOnDeleteWith;
            }

            foreach (var id in ids)
            {
                Files.Remove(id);
            }

            return Task.CompletedTask;
        }

        public Task<FileUploadScopeOptions> GetOptionsAsync(string scope) => throw new NotSupportedException();

        public Task<FileUploadResult> UploadFileAsync(FileUploadRequest request) => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string id) => throw new NotSupportedException();
    }

    private sealed class FakeMetadataService : ISalesRepDocumentMetadataService
    {
        public List<SalesRepDocumentMetadata> Saved { get; } = [];
        public bool FailOnSave { get; set; }

        public Task<IList<SalesRepDocumentMetadata>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
            => Task.FromResult<IList<SalesRepDocumentMetadata>>([.. Saved.Where(x => ids.Contains(x.Id))]);

        public Task SaveChangesAsync(IList<SalesRepDocumentMetadata> metadata)
        {
            if (FailOnSave)
            {
                throw new InvalidOperationException("Document metadata save failed.");
            }

            foreach (var model in metadata)
            {
                // The persistence layer generates the primary key for new rows.
                model.Id ??= Guid.NewGuid().ToString("N");

                Saved.RemoveAll(x => x.Id == model.Id);
                Saved.Add(model);
            }

            return Task.CompletedTask;
        }

        public Task<bool> SetPinnedAsync(string id, bool isPinned)
        {
            Saved.Single(x => x.Id == id).IsPinned = isPinned;
            return Task.FromResult(true);
        }

        public Task DeleteAsync(IList<string> ids, bool softDelete = false)
        {
            Saved.RemoveAll(x => ids.Contains(x.Id));
            return Task.CompletedTask;
        }
    }
}
