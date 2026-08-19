using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;
using Xunit;
using File = VirtoCommerce.FileExperienceApi.Core.Models.File;
using Module = VirtoCommerce.SalesRep.Web.Module;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

[Trait("Category", "Unit")]
public class SalesRepMapperTests
{
    private readonly SalesRepMapper _mapper = new();

    [Fact]
    public void ToDocument_MapsAllFields()
    {
        var file = CreateFile("file-1", name: "list.pdf", contentType: "application/pdf", size: 42);
        var metadata = CreateMetadata("file-1", name: "Pretty name");
        metadata.Id = "doc-1";
        metadata.CreatedBy = "creator";
        metadata.CreatedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        metadata.ModifiedBy = "editor";
        metadata.ModifiedDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        metadata.IsPinned = true;
        metadata.Summary = "summary";
        metadata.PageCount = 7;
        metadata.PreviewUrl = "preview.png";

        var document = _mapper.ToDocument(file, metadata);

        document.Id.Should().Be("doc-1");
        document.CreatedBy.Should().Be("creator");
        document.CreatedDate.Should().Be(metadata.CreatedDate);
        document.ModifiedBy.Should().Be("editor");
        document.ModifiedDate.Should().Be(metadata.ModifiedDate);
        document.FileId.Should().Be("file-1");
        document.Name.Should().Be("list.pdf");
        document.ContentType.Should().Be("application/pdf");
        document.Size.Should().Be(42);
        document.Url.Should().Be("/api/files/file-1");
        document.Category.Should().Be("Catalogs");
        document.IsPinned.Should().BeTrue();
        document.Summary.Should().Be("summary");
        document.PageCount.Should().Be(7);
        document.PreviewUrl.Should().Be("preview.png");
        document.DisplayName.Should().Be("Pretty name");
    }

    [Fact]
    public void ToDocument_EmptyMetadataName_FallsBackToTheFileName()
    {
        var document = _mapper.ToDocument(CreateFile("file-1", name: "list.pdf"), CreateMetadata("file-1", name: null));

        document.DisplayName.Should().Be("list.pdf");
    }

    [Fact]
    public void ToDocument_NullSource_ReturnsNull()
    {
        _mapper.ToDocument(null, CreateMetadata("file-1")).Should().BeNull();
        _mapper.ToDocument(CreateFile("file-1"), null).Should().BeNull();
    }

    [Fact]
    public void ToDocuments_PairsByFileId_SkipsMissingAndForeignScopeFiles()
    {
        var libraryFile = CreateFile("file-1");
        var foreignFile = CreateFile("file-2", scope: "product-images");
        var paired = CreateMetadata("FILE-1");
        var foreign = CreateMetadata("file-2");
        var orphan = CreateMetadata("file-3");

        var documents = _mapper.ToDocuments([libraryFile, foreignFile], [paired, foreign, orphan]);

        // Pairing is case-insensitive on the file id; a foreign-scope or missing file drops its row.
        documents.Should().ContainSingle().Which.FileId.Should().Be("file-1");
    }

    [Fact]
    public void ToDocuments_NullSource_ReturnsNull()
    {
        _mapper.ToDocuments(null, [CreateMetadata("file-1")]).Should().BeNull();
        _mapper.ToDocuments([CreateFile("file-1")], null).Should().BeNull();
    }

    [Fact]
    public void Module_Initialize_RegistersSalesRepMapper_AsSingleton()
    {
        var services = new ServiceCollection();
        var module = new Module { Configuration = new ConfigurationBuilder().Build() };

        module.Initialize(services);

        var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(ISalesRepMapper));

        descriptor.Should().NotBeNull();
        descriptor.ImplementationType.Should().Be<SalesRepMapper>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    private static File CreateFile(string id, string scope = ModuleConstants.DocumentsScope, string name = "list.pdf", string contentType = "application/pdf", long size = 1)
        => new() { Id = id, Scope = scope, Name = name, ContentType = contentType, Size = size };

    private static SalesRepDocumentMetadata CreateMetadata(string fileId, string name = "Pretty name")
        => new() { FileId = fileId, Name = name, Category = "Catalogs" };
}
