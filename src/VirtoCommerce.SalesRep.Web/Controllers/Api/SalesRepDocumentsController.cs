using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Authorization;
using FileModel = VirtoCommerce.FileExperienceApi.Core.Models.File;
using Permissions = VirtoCommerce.SalesRep.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.SalesRep.Web.Controllers.Api;

// Write endpoints use the declarative platform permission policy (documents:write, Administrator passes).
// Read endpoints must accept read OR write OR Administrator — a single-permission [Authorize] cannot express
// that OR, so they are [Authorize] (authenticated) + an explicit check of the same
// SalesRepDocumentAuthorizationRequirement the generic file surfaces run: one enforcement implementation.
[Authorize]
[Route("api/sales-rep/documents")]
public class SalesRepDocumentsController : Controller
{
    private readonly ISalesRepDocumentService _documentService;
    private readonly ISalesRepDocumentSearchService _documentSearchService;
    private readonly ISalesRepDocumentMetadataService _documentMetadataService;
    private readonly IAuthorizationService _authorizationService;

    public SalesRepDocumentsController(
        ISalesRepDocumentService documentService,
        ISalesRepDocumentSearchService documentSearchService,
        ISalesRepDocumentMetadataService documentMetadataService,
        IAuthorizationService authorizationService)
    {
        _documentService = documentService;
        _documentSearchService = documentSearchService;
        _documentMetadataService = documentMetadataService;
        _authorizationService = authorizationService;
    }

    [HttpPost("")]
    [Authorize(Permissions.DocumentsWrite)]
    [RequestSizeLimit(ModuleConstants.Documents.MaxFileSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = ModuleConstants.Documents.MaxFileSize)]
    public async Task<ActionResult<SalesRepDocument>> Upload(
        IFormFile file,
        [FromForm] string category,
        [FromForm] string name = null,
        [FromForm] string summary = null,
        [FromForm] int? pageCount = null,
        [FromForm] string previewUrl = null)
    {
        if (file == null)
        {
            return BadRequest("File is required.");
        }

        category ??= Request.Query["category"];

        SalesRepDocumentMetadata metadata = null;
        if (name != null || summary != null || pageCount != null || previewUrl != null)
        {
            metadata = AbstractTypeFactory<SalesRepDocumentMetadata>.TryCreateInstance();
            metadata.Name = name;
            metadata.Summary = summary;
            metadata.PageCount = pageCount;
            metadata.PreviewUrl = previewUrl;
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var document = await _documentService.UploadAsync(stream, file.FileName, category, metadata);
            return Ok(document);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("search")]
    public async Task<ActionResult<SalesRepDocumentSearchResult>> Search([FromBody] SalesRepDocumentSearchCriteria criteria)
    {
        if (!await AuthorizeReadAsync())
        {
            return Forbid();
        }

        var result = await _documentSearchService.SearchAsync(criteria ?? AbstractTypeFactory<SalesRepDocumentSearchCriteria>.TryCreateInstance());
        return Ok(result);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<SalesRepDocumentCategory[]>> GetCategories([FromQuery] string keyword = null)
    {
        if (!await AuthorizeReadAsync())
        {
            return Forbid();
        }

        var result = await _documentSearchService.GetCategoriesAsync(keyword);
        return Ok(result);
    }

    // [AllowAnonymous] on the two storefront-facing reads: the platform's default [Authorize] policy rejects
    // customer users (role __customer) before the action runs, and sales reps are customer users. The explicit
    // AuthorizeReadAsync call below replaces the policy and still denies anonymous (XFile FileUploadController precedent).
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult> Download([FromRoute] string id)
    {
        var document = await _documentService.GetAsync(id);

        if (document == null)
        {
            return NotFound();
        }

        if (!await AuthorizeReadAsync(document))
        {
            return Forbid();
        }

        var stream = await _documentService.OpenReadAsync(id);

        if (stream == null)
        {
            return NotFound();
        }

        return File(stream, document.ContentType ?? "application/octet-stream", document.Name);
    }

    [HttpGet("{id}/info")]
    [AllowAnonymous]
    public async Task<ActionResult<SalesRepDocument>> GetInfo([FromRoute] string id)
    {
        var document = await _documentService.GetAsync(id);

        if (document == null)
        {
            return NotFound();
        }

        if (!await AuthorizeReadAsync(document))
        {
            return Forbid();
        }

        return Ok(document);
    }

    [HttpPut("{id}/metadata")]
    [Authorize(Permissions.DocumentsWrite)]
    public async Task<ActionResult<SalesRepDocument>> UpdateMetadata([FromRoute] string id, [FromBody] SalesRepDocumentMetadata metadata)
    {
        if (metadata == null)
        {
            return BadRequest("Metadata is required.");
        }

        var document = await _documentService.GetAsync(id);

        if (document == null)
        {
            return NotFound();
        }

        metadata.Id = id;

        try
        {
            await _documentMetadataService.SaveAsync([metadata]);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }

        var result = await _documentService.GetAsync(id);
        return Ok(result);
    }

    // Pin state is exclusively these endpoints' concern (news module archive/unarchive REST shape);
    // the metadata PUT above never changes it.
    [HttpPost("{id}/pin")]
    [Authorize(Permissions.DocumentsWrite)]
    public Task<ActionResult<SalesRepDocument>> Pin([FromRoute] string id)
    {
        return SetPinnedAsync(id, isPinned: true);
    }

    [HttpPost("{id}/unpin")]
    [Authorize(Permissions.DocumentsWrite)]
    public Task<ActionResult<SalesRepDocument>> Unpin([FromRoute] string id)
    {
        return SetPinnedAsync(id, isPinned: false);
    }

    private async Task<ActionResult<SalesRepDocument>> SetPinnedAsync(string id, bool isPinned)
    {
        var document = await _documentService.GetAsync(id);

        if (document == null)
        {
            return NotFound();
        }

        await _documentMetadataService.SetPinnedAsync(id, isPinned);

        var result = await _documentService.GetAsync(id);
        return Ok(result);
    }

    [HttpDelete("")]
    [Authorize(Permissions.DocumentsWrite)]
    public async Task<ActionResult> Delete([FromQuery] string[] ids)
    {
        await _documentService.DeleteAsync(ids);
        return NoContent();
    }

    private async Task<bool> AuthorizeReadAsync(SalesRepDocument document = null)
    {
        FileModel file = null;

        if (document != null)
        {
            file = AbstractTypeFactory<FileModel>.TryCreateInstance();
            file.Id = document.Id;
            file.Scope = ModuleConstants.DocumentsScope;
            file.Name = document.Name;
            file.ContentType = document.ContentType;
            file.Size = document.Size;
        }

        var requirement = new SalesRepDocumentAuthorizationRequirement(file, Permissions.DocumentsRead);
        var result = await _authorizationService.AuthorizeAsync(User, file, requirement);

        return result.Succeeded;
    }
}
