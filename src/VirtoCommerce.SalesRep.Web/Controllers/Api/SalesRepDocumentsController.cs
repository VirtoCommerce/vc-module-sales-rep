using System;
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

// One permission per endpoint: read means read, write means write; the seeded Documents Manager role carries
// both. Administrators pass every permission via the platform's authorization handler.
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

    // Step 2 of the two-step upload: registers a file already uploaded to the sales-rep-documents scope (POST /api/files/{scope}).
    [HttpPost("")]
    [Authorize(Permissions.DocumentsWrite)]
    public async Task<ActionResult<SalesRepDocument>> Create([FromBody] SalesRepDocumentCreateRequest request)
    {
        if (string.IsNullOrEmpty(request?.FileId))
        {
            return BadRequest("File id is required.");
        }

        var metadata = AbstractTypeFactory<SalesRepDocumentMetadata>.TryCreateInstance();
        metadata.Name = request.Name;
        metadata.Summary = request.Summary;
        metadata.PageCount = request.PageCount;
        metadata.PreviewUrl = request.PreviewUrl;

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
    [Authorize(Permissions.DocumentsRead)]
    public async Task<ActionResult<SalesRepDocumentSearchResult>> Search([FromBody] SalesRepDocumentSearchCriteria criteria)
    {
        var result = await _documentSearchService.SearchAsync(criteria ?? AbstractTypeFactory<SalesRepDocumentSearchCriteria>.TryCreateInstance());
        return Ok(result);
    }

    [HttpGet("categories")]
    [Authorize(Permissions.DocumentsRead)]
    public async Task<ActionResult<SalesRepDocumentCategory[]>> GetCategories([FromQuery] string keyword = null)
    {
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
            return result != null ? Ok(result) : NotFound();
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
        var found = await _documentMetadataService.SetPinnedAsync(id, isPinned);

        return found ? NoContent() : NotFound();
    }

    [HttpDelete("")]
    [Authorize(Permissions.DocumentsWrite)]
    public async Task<ActionResult> Delete([FromQuery] string[] ids)
    {
        await _documentService.DeleteAsync(ids);
        return NoContent();
    }
}
