using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrdersQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepOrdersQuery, SalesRepOrderSearchResult>
{
    private readonly ISalesRepCustomerOrderSearchService _customerOrderSearchService;
    private readonly ISalesRepOrderResponseGroupParser _responseGroupParser;
    private readonly ISalesRepOrderStatusService _statusService;

    public SalesRepOrdersQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepCustomerOrderSearchService customerOrderSearchService,
        ISalesRepOrderResponseGroupParser responseGroupParser,
        ISalesRepOrderStatusService statusService)
        : base(roleResolver, membershipSearchService)
    {
        _customerOrderSearchService = customerOrderSearchService;
        _responseGroupParser = responseGroupParser;
        _statusService = statusService;
    }

    public virtual async Task<SalesRepOrderSearchResult> Handle(SalesRepOrdersQuery request, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<SalesRepOrderSearchResult>.TryCreateInstance();

        if (string.IsNullOrEmpty(request.UserId))
        {
            return result;
        }

        var organizationIds = await GetVisibleOrganizationIdsAsync(request);
        if (organizationIds.Length == 0)
        {
            return result;
        }

        string[] statuses = null;
        if (request.Statuses?.Count > 0)
        {
            statuses = await _statusService.ResolveOrderStatusesAsync(request.StoreId, request.Statuses);
            if (statuses.Length == 0)
            {
                return result;
            }
        }

        var criteria = BuildSearchCriteria(request, organizationIds, statuses);

        var searchResult = await _customerOrderSearchService.SearchAsync(criteria);

        result.TotalCount = searchResult.TotalCount;
        result.Results = searchResult.Results
            .Select(SalesRepOrder.FromOrder)
            .ToList();

        return result;
    }

    protected virtual CustomerOrderSearchCriteria BuildSearchCriteria(SalesRepOrdersQuery request, string[] organizationIds, string[] statuses)
    {
        var criteria = request.GetSearchCriteria<CustomerOrderSearchCriteria>();
        criteria.OrganizationIds = organizationIds;
        criteria.CustomerId = request.UserId;
        criteria.StoreIds = string.IsNullOrEmpty(request.StoreId) ? null : [request.StoreId];
        criteria.ResponseGroup = _responseGroupParser.GetResponseGroup(request.IncludeFields);

        if (statuses?.Length > 0)
        {
            criteria.Statuses = statuses;
        }

        if (string.IsNullOrEmpty(criteria.Sort))
        {
            criteria.Sort = "createdDate:desc";
        }

        return criteria;
    }

    protected virtual async Task<string[]> GetVisibleOrganizationIdsAsync(SalesRepOrdersQuery request)
    {
        if (!string.IsNullOrEmpty(request.OrganizationId))
        {
            return await ServesOrganizationAsync(request.UserId, request.OrganizationId) ? [request.OrganizationId] : [];
        }

        return await GetServedOrganizationIdsAsync(request.UserId);
    }
}
