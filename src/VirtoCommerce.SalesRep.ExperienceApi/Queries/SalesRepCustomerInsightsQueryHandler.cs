using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerInsightsQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomerInsightsQuery, SalesRepCustomerInsightsContext>
{
    private readonly ISalesRepCustomerInsightsService _insightsService;

    public SalesRepCustomerInsightsQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepCustomerInsightsService insightsService)
        : base(organizationAccessService)
    {
        _insightsService = insightsService;
    }

    public virtual async Task<SalesRepCustomerInsightsContext> Handle(SalesRepCustomerInsightsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId))
        {
            return null;
        }

        var organizationIds = await OrganizationAccessService.GetVisibleOrganizationIdsAsync(request.UserId, request.OrganizationId);
        if (organizationIds.Count == 0)
        {
            return null;
        }

        // "No insights provider configured" (analytics module absent or unconfigured) is an expected state, not an error.
        if (!await _insightsService.IsAvailableAsync(request.StoreId))
        {
            return null;
        }

        var result = AbstractTypeFactory<SalesRepCustomerInsightsContext>.TryCreateInstance();
        result.OrganizationIds = organizationIds;
        result.StoreId = request.StoreId;
        result.CultureName = request.CultureName;
        result.From = request.Period?.From;
        result.To = request.Period?.To;
        return result;
    }
}
