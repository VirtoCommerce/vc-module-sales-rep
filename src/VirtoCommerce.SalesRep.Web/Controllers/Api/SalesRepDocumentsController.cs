using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation;
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
// so they use [Authorize] + an explicit SalesRepDocumentPermissions.HasReadAccess check — the same read matrix
// SalesRepDocumentAuthorizationHandler enforces on the GraphQL queries and the generic file surfaces.
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

    // Step 2 of the two-step upload: the file is first uploaded to the sales-rep-documents scope via the
    // file-experience-api endpoint (POST /api/files/{scope}), then registered in the library here.
    [HttpPost("")]
    [Authorize(Permissions.DocumentsWrite)]
    public async Task<ActionResult<SalesRepDocument>> Create([FromBody] SalesRepDocumentCreateRequest request)
    {
        if (string.IsNullOrEmpty(request?.FileId))
        {
            return BadRequest("File id is required.");
        }

        SalesRepDocumentMetadata metadata = null;
        if (request.Name != null || request.Summary != null || request.PageCount != null || request.PreviewUrl != null)
        {
            metadata = AbstractTypeFactory<SalesRepDocumentMetadata>.TryCreateInstance();
            metadata.Name = request.Name;
            metadata.Summary = request.Summary;
            metadata.PageCount = request.PageCount;
            metadata.PreviewUrl = request.PreviewUrl;
        }

        try
        {
            var document = await _documentService.CreateAsync(request.FileId, request.Category, metadata);
            return Ok(document);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or ValidationException)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("search")]
    public async Task<ActionResult<SalesRepDocumentSearchResult>> Search([FromBody] SalesRepDocumentSearchCriteria criteria)
    {
        if (!User.HasReadAccess())
        {
            return Forbid();
        }

        var result = await _documentSearchService.SearchAsync(criteria ?? AbstractTypeFactory<SalesRepDocumentSearchCriteria>.TryCreateInstance());
        return Ok(result);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<SalesRepDocumentCategory[]>> GetCategories([FromQuery] string keyword = null)
    {
        if (!User.HasReadAccess())
        {
            return Forbid();
        }

        var result = await _documentSearchService.GetCategoriesAsync(keyword);
        return Ok(result);
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
        catch (Exception exception) when (exception is ArgumentException or ValidationException)
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
