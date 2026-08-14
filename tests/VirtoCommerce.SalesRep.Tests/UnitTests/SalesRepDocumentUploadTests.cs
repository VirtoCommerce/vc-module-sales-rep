using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.AssetsModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// Upload validation of <see cref="SalesRepDocumentService"/> in isolation (in-memory blob provider, dictionary
/// asset-entry/metadata doubles): extension white-listing, size limit (seekable fast path + bounded copy),
/// category sanitization (path traversal), blob-name randomization, and blob rollback on downstream failures.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepDocumentUploadTests
{
    private readonly InMemoryBlobStorageProvider _blobProvider = new();
    private readonly FakeAssetEntryService _assetEntryService = new();
    private readonly FakeMetadataService _metadataService = new();
    private readonly FakeFileExtensionService _fileExtensionService = new([".pdf", ".xlsx"]);

    [Fact]
    public async Task Upload_DisallowedExtension_ThrowsAndWritesNothing()
    {
        var service = CreateService();

        var upload = () => service.UploadAsync(Content("x"), "malware.exe", "Catalogs");

        await upload.Should().ThrowAsync<InvalidOperationException>().WithMessage("*.exe*not allowed*");
        _blobProvider.BlobUrls.Should().BeEmpty();
        _assetEntryService.Entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("../etc")]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    [InlineData("bad|name")]
    [InlineData("this-category-is-over-32-chars-long")] // > ModuleConstants.Documents.CategoryMaxLength
    public async Task Upload_InvalidCategory_ThrowsAndWritesNothing(string category)
    {
        var service = CreateService();

        var upload = () => service.UploadAsync(Content("x"), "list.pdf", category);

        await upload.Should().ThrowAsync<ArgumentException>();
        _blobProvider.BlobUrls.Should().BeEmpty();
        _assetEntryService.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_SeekableStreamOverLimit_ThrowsBeforeWriting()
    {
        var service = CreateService(maxFileSize: 8);

        var upload = () => service.UploadAsync(Content("123456789"), "list.pdf", "Catalogs");

        await upload.Should().ThrowAsync<InvalidOperationException>().WithMessage("*exceeds*");
        _blobProvider.BlobUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_NonSeekableStreamOverLimit_AbortsAndRemovesPartialBlob()
    {
        var service = CreateService(maxFileSize: 8);
        await using var stream = new NonSeekableStream(Content("123456789"));

        var upload = () => service.UploadAsync(stream, "list.pdf", "Catalogs");

        await upload.Should().ThrowAsync<InvalidOperationException>().WithMessage("*exceeds*");
        _blobProvider.BlobUrls.Should().BeEmpty();
        _assetEntryService.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_BlobName_RandomizedButKeepsSlugAndExtension()
    {
        var service = CreateService();

        var first = await service.UploadAsync(Content("a"), "Price List 2026.PDF", "Catalogs");
        var second = await service.UploadAsync(Content("b"), "Price List 2026.PDF", "Catalogs");

        // Blobs are stored flat under the library root — the category is metadata, not a path segment.
        var urls = _assetEntryService.Entries.Values.Select(x => x.BlobInfo.RelativeUrl).ToList();
        urls.Should().HaveCount(2).And.OnlyHaveUniqueItems();
        urls.Should().AllSatisfy(url =>
            url.Should().MatchRegex($"^{ModuleConstants.DocumentsScope}/price-list-2026-[0-9a-f]{{8}}\\.pdf$"));

        // The human-readable name survives untouched for display and download.
        first.Name.Should().Be("Price List 2026.PDF");
        second.Name.Should().Be("Price List 2026.PDF");
    }

    [Fact]
    public async Task Upload_Succeeds_MapsModelAndStoresEverything()
    {
        var service = CreateService();
        var metadata = new SalesRepDocumentMetadata { Summary = "Spring catalog", PageCount = 42, PreviewUrl = "https://cdn/preview.png" };

        var document = await service.UploadAsync(Content("payload"), "catalog.pdf", "Catalogs", metadata);

        document.Name.Should().Be("catalog.pdf");
        document.DisplayName.Should().Be("catalog.pdf"); // no metadata name → falls back to the file name
        document.Category.Should().Be("Catalogs");
        document.IsPinned.Should().BeFalse();
        document.ContentType.Should().Be("application/pdf");
        document.Size.Should().Be("payload".Length);
        document.Url.Should().Be($"/api/sales-rep/documents/{document.Id}");
        document.Summary.Should().Be("Spring catalog");
        document.PageCount.Should().Be(42);
        document.PreviewUrl.Should().Be("https://cdn/preview.png");

        var entry = _assetEntryService.Entries.Should().ContainSingle().Subject.Value;
        entry.Group.Should().Be(ModuleConstants.DocumentsScope);
        entry.BlobInfo.Name.Should().Be("catalog.pdf");
        _blobProvider.Exists(entry.BlobInfo.RelativeUrl).Should().BeTrue();

        var saved = _metadataService.Saved.Should().ContainSingle().Subject;
        saved.Id.Should().Be(document.Id);
        saved.Category.Should().Be("Catalogs");
    }

    [Fact]
    public async Task Upload_WithoutMetadata_StillStoresTheCategoryInMetadata()
    {
        var service = CreateService();

        var document = await service.UploadAsync(Content("x"), "list.pdf", " Catalogs ");

        document.Category.Should().Be("Catalogs"); // trimmed
        var saved = _metadataService.Saved.Should().ContainSingle().Subject;
        saved.Id.Should().Be(document.Id);
        saved.Category.Should().Be("Catalogs");
    }

    [Fact]
    public async Task Upload_WithMetadataName_UsesItAsTheDisplayName()
    {
        var service = CreateService();

        var document = await service.UploadAsync(Content("x"), "list.pdf", "Catalogs", new SalesRepDocumentMetadata { Name = "Spring price list" });

        document.Name.Should().Be("list.pdf");
        document.DisplayName.Should().Be("Spring price list");
    }

    [Fact]
    public async Task Upload_AssetEntrySaveFails_RollsBackBlob()
    {
        _assetEntryService.FailOnSave = true;
        var service = CreateService();

        var upload = () => service.UploadAsync(Content("x"), "list.pdf", "Catalogs");

        await upload.Should().ThrowAsync<InvalidOperationException>().WithMessage("*save failed*");
        _blobProvider.BlobUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_MetadataSaveFails_RollsBackBlobAndEntry()
    {
        _metadataService.FailOnSave = true;
        var service = CreateService();

        var upload = () => service.UploadAsync(Content("x"), "list.pdf", "Catalogs", new SalesRepDocumentMetadata { Summary = "s" });

        await upload.Should().ThrowAsync<InvalidOperationException>().WithMessage("*metadata save failed*");
        _blobProvider.BlobUrls.Should().BeEmpty();
        _assetEntryService.Entries.Should().BeEmpty();
    }

    private SalesRepDocumentService CreateService(long? maxFileSize = null)
        => new TestableDocumentService(_blobProvider, _assetEntryService, _metadataService, _fileExtensionService, maxFileSize);

    private static MemoryStream Content(string text) => new(Encoding.UTF8.GetBytes(text));

    private sealed class TestableDocumentService : SalesRepDocumentService
    {
        private readonly long? _maxFileSize;

        public TestableDocumentService(
            IBlobStorageProvider blobStorageProvider,
            IAssetEntryService assetEntryService,
            ISalesRepDocumentMetadataService metadataService,
            IFileExtensionService fileExtensionService,
            long? maxFileSize)
            : base(blobStorageProvider, assetEntryService, metadataService, fileExtensionService)
        {
            _maxFileSize = maxFileSize;
        }

        protected override long MaxFileSize => _maxFileSize ?? base.MaxFileSize;
    }

    private sealed class FakeAssetEntryService : IAssetEntryService
    {
        public Dictionary<string, AssetEntry> Entries { get; } = [];
        public bool FailOnSave { get; set; }

        public Task<IList<AssetEntry>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
            => Task.FromResult<IList<AssetEntry>>([.. ids.Where(Entries.ContainsKey).Select(id => Entries[id])]);

        public Task SaveChangesAsync(IList<AssetEntry> models)
        {
            if (FailOnSave)
            {
                throw new InvalidOperationException("Asset entry save failed.");
            }

            foreach (var model in models)
            {
                Entries[model.Id] = model;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(IList<string> ids, bool softDelete = false)
        {
            foreach (var id in ids)
            {
                Entries.Remove(id);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeMetadataService : ISalesRepDocumentMetadataService
    {
        public List<SalesRepDocumentMetadata> Saved { get; } = [];
        public bool FailOnSave { get; set; }

        public Task<IList<SalesRepDocumentMetadata>> GetByIdsAsync(IList<string> ids)
            => Task.FromResult<IList<SalesRepDocumentMetadata>>([.. Saved.Where(x => ids.Contains(x.Id))]);

        public Task SaveAsync(IList<SalesRepDocumentMetadata> metadata)
        {
            if (FailOnSave)
            {
                throw new InvalidOperationException("Document metadata save failed.");
            }

            Saved.AddRange(metadata);
            return Task.CompletedTask;
        }

        public Task SetPinnedAsync(string id, bool isPinned)
        {
            Saved.Single(x => x.Id == id).IsPinned = isPinned;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(IList<string> ids)
        {
            Saved.RemoveAll(x => ids.Contains(x.Id));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFileExtensionService : IFileExtensionService
    {
        private readonly IList<string> _whiteList;

        public FakeFileExtensionService(IList<string> whiteList) => _whiteList = whiteList;

        public Task<IList<string>> GetWhiteListAsync() => Task.FromResult(_whiteList);

        public Task<IList<string>> GetBlackListAsync() => Task.FromResult<IList<string>>([]);

        public Task<bool> IsExtensionAllowedAsync(string path)
            => Task.FromResult(_whiteList.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableStream(MemoryStream inner) => _inner = inner;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
