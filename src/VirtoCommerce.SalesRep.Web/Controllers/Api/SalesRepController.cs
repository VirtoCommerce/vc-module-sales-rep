using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using Permissions = VirtoCommerce.SalesRep.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.SalesRep.Web.Controllers.Api;

[Authorize]
[Route("api/sales-rep")]
public class SalesRepController : Controller
{
    private readonly ISalesRepService _salesRepService;
    private readonly ISalesRepSearchService _salesRepSearchService;

    public SalesRepController(
        ISalesRepService salesRepService,
        ISalesRepSearchService salesRepSearchService)
    {
        _salesRepService = salesRepService;
        _salesRepSearchService = salesRepSearchService;
    }

    /// <summary>Search Sales Reps (union of global-role and per-organization reps).</summary>
    [HttpPost("search")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<SalesRepSearchResult>> Search([FromBody] SalesRepSearchCriteria criteria)
    {
        var result = await _salesRepSearchService.SearchAsync(criteria);
        return Ok(result);
    }

    /// <summary>Roles selectable for a Sales Rep (those granting "sales-rep:access"); seeds a default if none.</summary>
    [HttpGet("roles")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<IList<SalesRepRole>>> GetRoles()
    {
        var result = await _salesRepService.GetRolesAsync();
        return Ok(result);
    }

    /// <summary>Get a Sales Rep aggregate by contact member id.</summary>
    [HttpGet("{id}")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<SalesRepDetails>> Get([FromRoute] string id)
    {
        var result = await _salesRepService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Create a new Sales Rep (contact + account + organization memberships).</summary>
    [HttpPost("")]
    [Authorize(Permissions.Create)]
    public async Task<ActionResult<SalesRepDetails>> Create([FromBody] SalesRepDetails salesRep)
    {
        salesRep.Id = null;
        var result = await _salesRepService.SaveChangesAsync(salesRep);
        return Ok(result);
    }

    /// <summary>Update an existing Sales Rep.</summary>
    [HttpPut("")]
    [Authorize(Permissions.Update)]
    public async Task<ActionResult<SalesRepDetails>> Update([FromBody] SalesRepDetails salesRep)
    {
        var result = await _salesRepService.SaveChangesAsync(salesRep);
        return Ok(result);
    }

    /// <summary>Delete Sales Reps by contact member ids (cascades to the security account).</summary>
    [HttpDelete("")]
    [Authorize(Permissions.Delete)]
    public async Task<ActionResult> Delete([FromQuery] string[] ids)
    {
        await _salesRepService.DeleteAsync(ids);
        return NoContent();
    }

    /// <summary>Block (lock out) the rep's account.</summary>
    [HttpPost("{id}/block")]
    [Authorize(Permissions.Update)]
    public async Task<ActionResult> Block([FromRoute] string id)
    {
        await _salesRepService.BlockAsync(id);
        return NoContent();
    }

    /// <summary>Unblock the rep's account.</summary>
    [HttpPost("{id}/unblock")]
    [Authorize(Permissions.Update)]
    public async Task<ActionResult> Unblock([FromRoute] string id)
    {
        await _salesRepService.UnblockAsync(id);
        return NoContent();
    }

    /// <summary>Set a new password for the rep's account.</summary>
    [HttpPost("{id}/password")]
    [Authorize(Permissions.Update)]
    public async Task<ActionResult> SetPassword([FromRoute] string id, [FromBody] SetPasswordRequest request)
    {
        await _salesRepService.SetPasswordAsync(id, request.Password);
        return NoContent();
    }
}
