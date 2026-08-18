using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.FileExperienceApi.Core.Models;
using VirtoCommerce.FileExperienceApi.Core.Services;
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
    public async Task Create_AlreadyOwnedFile_ThrowsAndSavesNothing()
    {
        var file = AddFile();
        file.OwnerEntityId = "other-document";
        file.OwnerEntityType = nameof(SalesRepDocumentMetadata);
        var service = CreateService();

        var create = () => service.CreateAsync(file.Id, "Catalogs");

        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already belongs*");
        _metadataService.Saved.Should().BeEmpty();
    }

    // The rejected characters are an explicit OS-independent set — every case must fail on Windows and Linux alike.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("../etc")]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    [InlineData("bad|name")]
    [InlineData("bad:name")]
    [InlineData("bad\tname")]
    [InlineData("this-category-is-over-32-chars-long")] // > ModuleConstants.Documents.CategoryMaxLength
    public async Task Create_InvalidCategory_ThrowsAndSavesNothing(string category)
    {
        var file = AddFile();
        var service = CreateService();

        var create = () => service.CreateAsync(file.Id, category);

        await create.Should().ThrowAsync<ArgumentException>();
        _metadataService.Saved.Should().BeEmpty();
        file.OwnerEntityId.Should().BeNull();
    }

    [Fact]
    public async Task Create_Succeeds_MapsModelSavesMetadataAndTakesOwnership()
    {
        var file = AddFile(name: "catalog.pdf", contentType: "application/pdf", size: 7);
        var service = CreateService();
        var metadata = new SalesRepDocumentMetadata { Summary = "Spring catalog", PageCount = 42, PreviewUrl = "https://cdn/preview.png" };

        var document = await service.CreateAsync(file.Id, " Catalogs ", metadata);

        var saved = _metadataService.Saved.Should().ContainSingle().Subject;
        saved.Id.Should().NotBeNullOrEmpty("the metadata row gets its own generated id");
        saved.FileId.Should().Be(file.Id);
        saved.Category.Should().Be("Catalogs"); // trimmed

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
        file.OwnerEntityType.Should().Be(nameof(SalesRepDocumentMetadata));
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
        (await service.GetAsync("missing")).Should().BeNull();

        // Metadata whose file has vanished from the store must not surface as a document.
        _metadataService.Saved.Add(new SalesRepDocumentMetadata { Id = "orphan", FileId = "gone", Category = "Catalogs" });
        (await service.GetAsync("orphan")).Should().BeNull();
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

    private SalesRepDocumentService CreateService() => new(_fileUploadService, _metadataService);

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
        _fileUploadService.Files[file.Id] = file;
        return file;
    }

    private sealed class FakeFileUploadService : IFileUploadService
    {
        public Dictionary<string, File> Files { get; } = [];
        public bool FailOnSave { get; set; }

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

        public Task<IList<SalesRepDocumentMetadata>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
            => Task.FromResult<IList<SalesRepDocumentMetadata>>([.. Saved.Where(x => ids.Contains(x.Id))]);

        public Task SaveChangesAsync(IList<SalesRepDocumentMetadata> metadata)
        {
            foreach (var model in metadata)
            {
                // The persistence layer generates the primary key for new rows.
                model.Id ??= Guid.NewGuid().ToString("N");

                Saved.RemoveAll(x => x.Id == model.Id);
                Saved.Add(model);
            }

            return Task.CompletedTask;
        }

        public Task SetPinnedAsync(string id, bool isPinned)
        {
            Saved.Single(x => x.Id == id).IsPinned = isPinned;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(IList<string> ids, bool softDelete = false)
        {
            Saved.RemoveAll(x => ids.Contains(x.Id));
            return Task.CompletedTask;
        }
    }
}
