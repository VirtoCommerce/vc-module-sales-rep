using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using Permissions = VirtoCommerce.SalesRep.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.SalesRep.Web.Controllers.Api;

// Read endpoints need read OR write OR Administrator, which a single-permission [Authorize] cannot express,
// so they use [Authorize] + an explicit SalesRepDocumentPermissions.HasReadAccess check (the same .Core predicate the GraphQL resolver enforces).
[Authorize]
[Route("api/sales-rep/documents")]
public class SalesRepDocumentsController : Controller
{
    private readonly ISalesRepDocumentService _documentService;
    private readonly ISalesRepDocumentSearchService _documentSearchService;
    private readonly ISalesRepDocumentMetadataService _documentMetadataService;

    public SalesRepDocumentsController(
        ISalesRepDocumentService documentService,
        ISalesRepDocumentSearchService documentSearchService,
        ISalesRepDocumentMetadataService documentMetadataService)
    {
        _documentService = documentService;
        _documentSearchService = documentSearchService;
        _documentMetadataService = documentMetadataService;
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
        if (!SalesRepDocumentPermissions.HasReadAccess(User))
        {
            return Forbid();
        }

        var result = await _documentSearchService.SearchAsync(criteria ?? AbstractTypeFactory<SalesRepDocumentSearchCriteria>.TryCreateInstance());
        return Ok(result);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<SalesRepDocumentCategory[]>> GetCategories([FromQuery] string keyword = null)
    {
        if (!SalesRepDocumentPermissions.HasReadAccess(User))
        {
            return Forbid();
        }

        var result = await _documentSearchService.GetCategoriesAsync(keyword);
        return Ok(result);
    }

    // [AllowAnonymous]: the default [Authorize] policy rejects __customer users before the action runs, and sales reps are customer users.
    // The explicit HasReadAccess check below replaces the policy and still denies anonymous.
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult> Download([FromRoute] string id)
    {
        var document = await _documentService.GetAsync(id);

        if (document == null)
        {
            return NotFound();
        }

        if (!SalesRepDocumentPermissions.HasReadAccess(User))
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

        if (!SalesRepDocumentPermissions.HasReadAccess(User))
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

        try
        {
            var result = await _documentService.UpdateMetadataAsync(id, metadata);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    // Pin state is exclusively these endpoints' concern; the metadata PUT above never changes it.
    [HttpPost("{id}/pin")]
    [Authorize(Permissions.DocumentsWrite)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public Task<ActionResult> Pin([FromRoute] string id)
    {
        return SetPinnedAsync(id, isPinned: true);
    }

    [HttpPost("{id}/unpin")]
    [Authorize(Permissions.DocumentsWrite)]
    [ProducesResponseType(typeof(void), StatusCodes.Status204NoContent)]
    public Task<ActionResult> Unpin([FromRoute] string id)
    {
        return SetPinnedAsync(id, isPinned: false);
    }

    private async Task<ActionResult> SetPinnedAsync(string id, bool isPinned)
    {
        try
        {
            await _documentMetadataService.SetPinnedAsync(id, isPinned);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpDelete("")]
    [Authorize(Permissions.DocumentsWrite)]
    public async Task<ActionResult> Delete([FromQuery] string[] ids)
    {
        await _documentService.DeleteAsync(ids);
        return NoContent();
    }
}
