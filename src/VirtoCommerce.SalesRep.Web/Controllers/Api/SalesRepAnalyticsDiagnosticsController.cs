using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Web.Controllers.Api;

[Authorize]
[Route("api/sales-rep")]
public class SalesRepAnalyticsDiagnosticsController : Controller
{
    private readonly ISalesRepAnalyticsDiagnosticsService _diagnosticsService;

    public SalesRepAnalyticsDiagnosticsController(ISalesRepAnalyticsDiagnosticsService diagnosticsService)
    {
        _diagnosticsService = diagnosticsService;
    }

    [HttpPost("analytics-diagnostics")]
    [Authorize(ModuleConstants.Security.Permissions.Diagnostics)]
    public async Task<ActionResult<AnalyticsDiagnosticsResult>> RunAnalyticsDiagnostics([FromQuery] string storeId, [FromQuery] bool includeLiveData = true)
    {
        var result = await _diagnosticsService.RunAsync(storeId, includeLiveData);
        return Ok(result);
    }
}
