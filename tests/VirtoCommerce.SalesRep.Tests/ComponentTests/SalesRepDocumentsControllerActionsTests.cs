using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.FileExperienceApi.Core.Models;
using VirtoCommerce.FileExperienceApi.Core.Services;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Web.Controllers.Api;
using Xunit;
using ModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// The documents REST controller invoked directly (no TestServer) against the real file/metadata services.
/// Authorization is declarative — one permission per endpoint via [Authorize], enforced by the platform's
/// permission handler (Administrator bypass included) — so this class asserts the declared attribute per action
/// and covers the Create action's mappings (missing fileId, optional metadata assembly, service-exception → 400).
/// File download authorization runs on the file-experience-api endpoint through
/// SalesRepDocumentAuthorizationHandler — covered by its own tests, which own the permission matrix.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepDocumentsControllerActionsTests
{
    private const string DocumentsRead = ModuleConstants.Security.Permissions.DocumentsRead;
    private const string DocumentsWrite = ModuleConstants.Security.Permissions.DocumentsWrite;

    // ---- Declarative authorization: one permission per endpoint (read means read, write means write) ----

    [Theory]
    [InlineData(nameof(SalesRepDocumentsController.Search), DocumentsRead)]
    [InlineData(nameof(SalesRepDocumentsController.GetCategories), DocumentsRead)]
    [InlineData(nameof(SalesRepDocumentsController.Create), DocumentsWrite)]
    [InlineData(nameof(SalesRepDocumentsController.UpdateMetadata), DocumentsWrite)]
    [InlineData(nameof(SalesRepDocumentsController.Pin), DocumentsWrite)]
    [InlineData(nameof(SalesRepDocumentsController.Unpin), DocumentsWrite)]
    [InlineData(nameof(SalesRepDocumentsController.Delete), DocumentsWrite)]
    public void Action_DeclaresExactlyItsPermission(string actionName, string permission)
    {
        var attribute = typeof(SalesRepDocumentsController)
            .GetMethod(actionName)
            .GetCustomAttributes<AuthorizeAttribute>()
            .Single();

        attribute.Policy.Should().Be(permission);
    }

    // ---- Create action mappings ----

    [Fact]
    public async Task Create_MissingFileId_IsBadRequest()
    {
        using var ctx = SalesRepTestContext.Create();
        var controller = CreateController(ctx, WithPermissions(DocumentsWrite));

        (await controller.Create(null)).Result.Should().BeOfType<BadRequestObjectResult>();
        (await controller.Create(new SalesRepDocumentCreateRequest { Category = "Catalogs" })).Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_UnknownFileId_IsBadRequest()
    {
        using var ctx = SalesRepTestContext.Create();
        var controller = CreateController(ctx, WithPermissions(DocumentsWrite));

        (await controller.Create(new SalesRepDocumentCreateRequest { FileId = "no-such-file", Category = "Catalogs" }))
            .Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_AssemblesOptionalMetadataFieldsAndPersistsThem()
    {
        using var ctx = SalesRepTestContext.Create();
        var fileId = await UploadFileAsync(ctx, "Spec.pdf");
        var controller = CreateController(ctx, WithPermissions(DocumentsWrite));

        var document = (await controller.Create(new SalesRepDocumentCreateRequest
        {
            FileId = fileId,
            Category = "Specs",
            Name = "Pretty spec",
            Summary = "The summary",
            PageCount = 9,
            PreviewUrl = "https://example.test/preview.png",
        }))
            .Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<SalesRepDocument>().Subject;

        document.FileId.Should().Be(fileId);
        document.DisplayName.Should().Be("Pretty spec");
        document.Summary.Should().Be("The summary");
        document.PageCount.Should().Be(9);
        document.PreviewUrl.Should().Be("https://example.test/preview.png");

        var reloaded = await ctx.GetRequiredService<ISalesRepDocumentService>().GetByIdAsync(document.Id);
        reloaded.FileId.Should().Be(fileId, "the persisted metadata row must reference the step-1 uploaded file");
        reloaded.DisplayName.Should().Be("Pretty spec");
        reloaded.Summary.Should().Be("The summary");
        reloaded.PageCount.Should().Be(9);
        reloaded.PreviewUrl.Should().Be("https://example.test/preview.png");
    }

    [Fact]
    public async Task Create_ServiceRejectsAnInvalidCategory_IsBadRequest()
    {
        using var ctx = SalesRepTestContext.Create();
        var fileId = await UploadFileAsync(ctx, "Doc.pdf");
        var controller = CreateController(ctx, WithPermissions(DocumentsWrite));

        // A category carrying a path separator is genuinely rejected by the real SalesRepDocumentMetadataValidator
        // in the save pipeline (ValidationException), which the action maps to a 400 — no mock involved.
        (await controller.Create(new SalesRepDocumentCreateRequest { FileId = fileId, Category = "bad/category" }))
            .Result.Should().BeOfType<BadRequestObjectResult>();

        // Nothing was persisted for the rejected registration.
        (await ctx.GetRequiredService<ISalesRepDocumentSearchService>()
            .SearchAsync(new SalesRepDocumentSearchCriteria())).TotalCount.Should().Be(0);
    }

    // ---- Default-sort tie-break (isPinned:desc THEN createdDate:desc) at the orchestrator ----

    [Fact]
    public async Task Search_DefaultSort_PinnedOlderOutranksNewerUnpinned()
    {
        using var ctx = SalesRepTestContext.Create();

        var pinnedOlder = await ctx.UploadDocumentAsync("Pinned Older.pdf", "Catalogs");
        var newerUnpinned = await ctx.UploadDocumentAsync("Newer Unpinned.pdf", "Catalogs");

        // Deterministic ages: the pinned document is the OLDER of the two (createdDate:desc alone would rank it last).
        await ctx.SetDocumentCreatedDateAsync(pinnedOlder.Id, new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc));
        await ctx.SetDocumentCreatedDateAsync(newerUnpinned.Id, new System.DateTime(2026, 3, 1, 0, 0, 0, System.DateTimeKind.Utc));

        await ctx.GetRequiredService<ISalesRepDocumentMetadataService>().SetPinnedAsync(pinnedOlder.Id, isPinned: true);

        var result = await ctx.GetRequiredService<ISalesRepDocumentSearchService>()
            .SearchAsync(new SalesRepDocumentSearchCriteria());

        // Pinned-first wins over newest-first: the older pinned document leads the newer unpinned one.
        result.Results.Select(x => x.Id).Should().Equal(pinnedOlder.Id, newerUnpinned.Id);
        result.Results.First().IsPinned.Should().BeTrue();
    }

    /// <summary>Step 1 only: an uploaded, not-yet-registered library file.</summary>
    private static async Task<string> UploadFileAsync(SalesRepTestContext ctx, string fileName, string content = "content")
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var result = await ctx.GetRequiredService<IFileUploadService>().UploadFileAsync(new FileUploadRequest
        {
            Scope = ModuleConstants.DocumentsScope,
            UserId = "test-user",
            FileName = fileName,
            Stream = stream,
        });

        result.Succeeded.Should().BeTrue(result.ErrorMessage);
        return result.Id;
    }

    private static SalesRepDocumentsController CreateController(SalesRepTestContext ctx, ClaimsPrincipal user = null)
    {
        var controller = new SalesRepDocumentsController(
            ctx.GetRequiredService<ISalesRepDocumentService>(),
            ctx.GetRequiredService<ISalesRepDocumentSearchService>(),
            ctx.GetRequiredService<ISalesRepDocumentMetadataService>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user ?? Anonymous() },
        };
        return controller;
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal WithPermissions(params string[] permissions)
        => new(new ClaimsIdentity(
            permissions.Select(p => new Claim(PlatformConstants.Security.Claims.PermissionClaimType, p)),
            authenticationType: "Test"));

}
