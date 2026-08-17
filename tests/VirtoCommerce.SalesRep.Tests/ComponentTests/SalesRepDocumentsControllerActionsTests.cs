using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Web.Controllers.Api;
using Xunit;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// The documents REST controller invoked directly (no TestServer) against the real assets/metadata services, with
/// a crafted <see cref="ClaimsPrincipal"/> on the ControllerContext so the in-action <c>HasReadAccess → Forbid()</c>
/// guard runs. Covers the read-endpoint authorization matrix, the Download/GetInfo response branches, and the Upload
/// action's mappings (null file, query-string category fallback, optional metadata assembly, service-exception → 400).
/// </summary>
[Trait("Category", "Component")]
public class SalesRepDocumentsControllerActionsTests
{
    private const string DocumentsRead = ModuleConstants.Security.Permissions.DocumentsRead;
    private const string DocumentsWrite = ModuleConstants.Security.Permissions.DocumentsWrite;

    // ---- Gap 2: read-endpoint in-action authorization (crafted principals) ----

    [Fact]
    public async Task Reads_ForbidAnonymousAndAuthenticatedWithoutPermission()
    {
        using var ctx = SalesRepTestContext.Create();
        // Download/GetInfo look the document up first and 404 a missing one BEFORE the auth check, so a seeded
        // document is required to reach (and prove) their Forbid branch.
        var document = await UploadAsync(ctx, "Report.pdf", "Catalogs");

        foreach (var user in new[] { Anonymous(), AuthenticatedWithout() })
        {
            var controller = CreateController(ctx, user);

            (await controller.Download(document.Id)).Should().BeOfType<ForbidResult>();
            (await controller.GetInfo(document.Id)).Result.Should().BeOfType<ForbidResult>();
            (await controller.Search(new SalesRepDocumentSearchCriteria())).Result.Should().BeOfType<ForbidResult>();
            (await controller.GetCategories(null)).Result.Should().BeOfType<ForbidResult>();
        }
    }

    [Fact]
    public async Task Reads_AllowWithReadPermission()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await UploadAsync(ctx, "Report.pdf", "Catalogs");

        var controller = CreateController(ctx, WithPermissions(DocumentsRead));

        (await controller.Download(document.Id)).Should().NotBeOfType<ForbidResult>().And.BeOfType<FileStreamResult>();
        (await controller.GetInfo(document.Id)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.Search(new SalesRepDocumentSearchCriteria())).Result.Should().BeOfType<OkObjectResult>();
        (await controller.GetCategories(null)).Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Download_FullPrincipalMatrix()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await UploadAsync(ctx, "Report.pdf", "Catalogs");

        // Deny: anonymous and authenticated-without-permission.
        (await CreateController(ctx, Anonymous()).Download(document.Id)).Should().BeOfType<ForbidResult>();
        (await CreateController(ctx, AuthenticatedWithout()).Download(document.Id)).Should().BeOfType<ForbidResult>();

        // Allow: read, write (implies read), and Administrator.
        (await CreateController(ctx, WithPermissions(DocumentsRead)).Download(document.Id)).Should().BeOfType<FileStreamResult>();
        (await CreateController(ctx, WithPermissions(DocumentsWrite)).Download(document.Id)).Should().BeOfType<FileStreamResult>();
        (await CreateController(ctx, Administrator()).Download(document.Id)).Should().BeOfType<FileStreamResult>();
    }

    // ---- Gap 3: Download / GetInfo response behavior + branches ----

    [Fact]
    public async Task Download_StreamsTheFileWithContentTypeAndRawName()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await UploadAsync(ctx, "Guide.pdf", "Guides", content: "guide-bytes");

        var controller = CreateController(ctx, WithPermissions(DocumentsRead));

        var file = (await controller.Download(document.Id)).Should().BeOfType<FileStreamResult>().Subject;
        file.ContentType.Should().Be(document.ContentType);
        file.ContentType.Should().Be("application/pdf");
        file.FileDownloadName.Should().Be("Guide.pdf");

        using var reader = new StreamReader(file.FileStream);
        (await reader.ReadToEndAsync()).Should().Be("guide-bytes");
    }

    [Fact]
    public async Task Download_UnknownId_IsNotFound()
    {
        using var ctx = SalesRepTestContext.Create();
        await UploadAsync(ctx, "Only.pdf", "Catalogs");

        var controller = CreateController(ctx, WithPermissions(DocumentsRead));

        (await controller.Download("missing-id")).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetInfo_ReturnsTheMappedDocument()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await UploadAsync(ctx, "Info.pdf", "Guides", new SalesRepDocumentMetadata { Name = "Nice name", Summary = "About", PageCount = 2 });

        var controller = CreateController(ctx, WithPermissions(DocumentsRead));

        var info = (await controller.GetInfo(document.Id)).Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<SalesRepDocument>().Subject;
        info.Id.Should().Be(document.Id);
        info.Name.Should().Be("Info.pdf");
        info.DisplayName.Should().Be("Nice name");
        info.Category.Should().Be("Guides");
        info.Summary.Should().Be("About");
        info.PageCount.Should().Be(2);
        info.Url.Should().Be($"/api/sales-rep/documents/{document.Id}");
    }

    [Fact]
    public async Task GetInfo_UnknownId_IsNotFound()
    {
        using var ctx = SalesRepTestContext.Create();
        await UploadAsync(ctx, "Only.pdf", "Catalogs");

        var controller = CreateController(ctx, WithPermissions(DocumentsRead));

        (await controller.GetInfo("missing-id")).Result.Should().BeOfType<NotFoundResult>();
    }

    // ---- Gap 4: Upload action mappings ----

    [Fact]
    public async Task Upload_NullFile_IsBadRequest()
    {
        using var ctx = SalesRepTestContext.Create();
        var controller = CreateController(ctx, WithPermissions(DocumentsWrite));

        (await controller.Upload(file: null, category: "Catalogs")).Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Upload_UsesTheQueryStringCategoryWhenTheFormParamIsNull()
    {
        using var ctx = SalesRepTestContext.Create();
        var controller = CreateController(
            ctx,
            WithPermissions(DocumentsWrite),
            query: new QueryCollection(new Dictionary<string, StringValues> { ["category"] = "FromQuery" }));

        var document = (await controller.Upload(CreateFormFile("Doc.pdf", "content"), category: null))
            .Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<SalesRepDocument>().Subject;

        document.Category.Should().Be("FromQuery");

        // And it is the value that was persisted.
        var reloaded = await ctx.GetRequiredService<ISalesRepDocumentService>().GetAsync(document.Id);
        reloaded.Category.Should().Be("FromQuery");
    }

    [Fact]
    public async Task Upload_AssemblesOptionalMetadataFieldsAndPersistsThem()
    {
        using var ctx = SalesRepTestContext.Create();
        var controller = CreateController(ctx, WithPermissions(DocumentsWrite));

        var document = (await controller.Upload(
                CreateFormFile("Spec.pdf", "content"),
                category: "Specs",
                name: "Pretty spec",
                summary: "The summary",
                pageCount: 9,
                previewUrl: "https://example.test/preview.png"))
            .Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<SalesRepDocument>().Subject;

        document.DisplayName.Should().Be("Pretty spec");
        document.Summary.Should().Be("The summary");
        document.PageCount.Should().Be(9);
        document.PreviewUrl.Should().Be("https://example.test/preview.png");

        var reloaded = await ctx.GetRequiredService<ISalesRepDocumentService>().GetAsync(document.Id);
        reloaded.DisplayName.Should().Be("Pretty spec");
        reloaded.Summary.Should().Be("The summary");
        reloaded.PageCount.Should().Be(9);
        reloaded.PreviewUrl.Should().Be("https://example.test/preview.png");
    }

    [Fact]
    public async Task Upload_ServiceRejectsAnInvalidCategory_IsBadRequest()
    {
        using var ctx = SalesRepTestContext.Create();
        var controller = CreateController(ctx, WithPermissions(DocumentsWrite));

        // A category carrying a path separator is genuinely rejected by the real category validator inside the
        // service (ArgumentException), which the action maps to a 400 — no mock involved.
        (await controller.Upload(CreateFormFile("Doc.pdf", "content"), category: "bad/category"))
            .Result.Should().BeOfType<BadRequestObjectResult>();

        // Nothing was persisted for the rejected upload.
        (await ctx.GetRequiredService<ISalesRepDocumentSearchService>()
            .SearchAsync(new SalesRepDocumentSearchCriteria())).TotalCount.Should().Be(0);
    }

    // ---- Gap 5: default-sort tie-break (isPinned:desc THEN createdDate:desc) at the orchestrator ----

    [Fact]
    public async Task Search_DefaultSort_PinnedOlderOutranksNewerUnpinned()
    {
        using var ctx = SalesRepTestContext.Create();

        var pinnedOlder = await UploadAsync(ctx, "Pinned Older.pdf", "Catalogs");
        var newerUnpinned = await UploadAsync(ctx, "Newer Unpinned.pdf", "Catalogs");

        // Deterministic ages: the pinned document is the OLDER of the two (createdDate:desc alone would rank it last).
        await ctx.SetDocumentCreatedDateAsync(pinnedOlder.Id, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await ctx.SetDocumentCreatedDateAsync(newerUnpinned.Id, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        await ctx.GetRequiredService<ISalesRepDocumentMetadataService>().SetPinnedAsync(pinnedOlder.Id, isPinned: true);

        var result = await ctx.GetRequiredService<ISalesRepDocumentSearchService>()
            .SearchAsync(new SalesRepDocumentSearchCriteria());

        // Pinned-first wins over newest-first: the older pinned document leads the newer unpinned one.
        result.Results.Select(x => x.Id).Should().Equal(pinnedOlder.Id, newerUnpinned.Id);
        result.Results.First().IsPinned.Should().BeTrue();
    }

    private static SalesRepDocumentsController CreateController(
        SalesRepTestContext ctx,
        ClaimsPrincipal user = null,
        IQueryCollection query = null)
    {
        var controller = new SalesRepDocumentsController(
            ctx.GetRequiredService<ISalesRepDocumentService>(),
            ctx.GetRequiredService<ISalesRepDocumentSearchService>(),
            ctx.GetRequiredService<ISalesRepDocumentMetadataService>());

        var httpContext = new DefaultHttpContext { User = user ?? Anonymous() };
        if (query != null)
        {
            httpContext.Request.Query = query;
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal AuthenticatedWithout() => new(new ClaimsIdentity(claims: [], authenticationType: "Test"));

    private static ClaimsPrincipal WithPermissions(params string[] permissions)
        => new(new ClaimsIdentity(
            permissions.Select(p => new Claim(PlatformConstants.Security.Claims.PermissionClaimType, p)),
            authenticationType: "Test"));

    private static ClaimsPrincipal Administrator()
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, PlatformConstants.Security.SystemRoles.Administrator)],
            authenticationType: "Test"));

    private static IFormFile CreateFormFile(string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), baseStreamOffset: 0, length: bytes.Length, name: "file", fileName: fileName);
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
}
