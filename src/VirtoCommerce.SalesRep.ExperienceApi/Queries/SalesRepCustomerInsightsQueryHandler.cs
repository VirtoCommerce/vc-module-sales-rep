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

    private readonly ISalesRepStoreAccessService _storeAccessService;

    public SalesRepCustomerInsightsQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepCustomerInsightsService insightsService,
        ISalesRepStoreAccessService storeAccessService)
        : base(organizationAccessService)
    {
        _insightsService = insightsService;
        _storeAccessService = storeAccessService;
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

        // A named store is a claim, not a filter: it chooses whose analytics property is read, so it is
        // checked against the caller's own store before it is used.
        if (!await _storeAccessService.IsAllowedAsync(request.UserId, request.StoreId))
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
        result.From = request.Period?.From;
        result.To = request.Period?.To;
        return result;
    }
}
