using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using Permissions = VirtoCommerce.CustomerModule.Core.ModuleConstants.Security.Permissions;
using PlatformPermissions = VirtoCommerce.Platform.Core.PlatformConstants.Security.Permissions;

namespace VirtoCommerce.SalesRep.Web.Controllers.Api;

[Authorize]
[Route("api/sales-rep")]
public class SalesRepController : Controller
{
    private readonly ISalesRepService _salesRepService;
    private readonly ISalesRepSearchService _salesRepSearchService;
    private readonly ISalesRepDictionaryService _salesRepDictionaryService;

    public SalesRepController(
        ISalesRepService salesRepService,
        ISalesRepSearchService salesRepSearchService,
        ISalesRepDictionaryService salesRepDictionaryService)
    {
        _salesRepService = salesRepService;
        _salesRepSearchService = salesRepSearchService;
        _salesRepDictionaryService = salesRepDictionaryService;
    }

    [HttpPost("search")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<SalesRepSearchResult>> Search([FromBody] SalesRepSearchCriteria criteria)
    {
        var result = await _salesRepSearchService.SearchAsync(criteria);
        return Ok(result);
    }

    [HttpGet("roles")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<IList<SalesRepRole>>> GetRoles()
    {
        var result = await _salesRepService.GetRolesAsync();
        return Ok(result);
    }

    [HttpGet("dictionaries")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<SalesRepDictionaries>> GetDictionaries()
    {
        var result = await _salesRepDictionaryService.GetDictionariesAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Permissions.Read)]
    public async Task<ActionResult<SalesRepDetails>> Get([FromRoute] string id)
    {
        var result = await _salesRepService.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("")]
    [Authorize(Permissions.Create)]
    [Authorize(PlatformPermissions.SecurityCreate)]
    public async Task<ActionResult<SalesRepDetails>> Create([FromBody] SalesRepDetails salesRep)
    {
        salesRep.Id = null;
        await _salesRepService.SaveChangesAsync([salesRep]);
        var result = await _salesRepService.GetByIdAsync(salesRep.Id);
        return Ok(result);
    }

    [HttpPut("")]
    [Authorize(Permissions.Update)]
    [Authorize(PlatformPermissions.SecurityUpdate)]
    public async Task<ActionResult<SalesRepDetails>> Update([FromBody] SalesRepDetails salesRep)
    {
        await _salesRepService.SaveChangesAsync([salesRep]);
        var result = await _salesRepService.GetByIdAsync(salesRep.Id);
        return Ok(result);
    }

    [HttpDelete("")]
    [Authorize(Permissions.Delete)]
    [Authorize(PlatformPermissions.SecurityDelete)]
    public async Task<ActionResult> Delete([FromQuery] string[] ids)
    {
        await _salesRepService.DeleteAsync(ids);
        return NoContent();
    }

    [HttpPost("{id}/block")]
    [Authorize(PlatformPermissions.SecurityUpdate)]
    public async Task<ActionResult> Block([FromRoute] string id)
    {
        await _salesRepService.BlockAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/unblock")]
    [Authorize(PlatformPermissions.SecurityUpdate)]
    public async Task<ActionResult> Unblock([FromRoute] string id)
    {
        await _salesRepService.UnblockAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/password")]
    [Authorize(PlatformPermissions.SecurityUpdate)]
    public async Task<ActionResult> SetPassword([FromRoute] string id, [FromBody] SetPasswordRequest request)
    {
        await _salesRepService.SetPasswordAsync(id, request.Password);
        return NoContent();
    }
}
