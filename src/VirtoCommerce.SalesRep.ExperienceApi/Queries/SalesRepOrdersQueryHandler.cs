using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly ISalesRepOrderFilterRuleResolver _filterRuleResolver;
    private readonly ISalesRepOrderSortRuleResolver _sortRuleResolver;

    public SalesRepOrdersQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepCustomerOrderSearchService customerOrderSearchService,
        ISalesRepOrderResponseGroupParser responseGroupParser,
        ISalesRepOrderFilterRuleResolver filterRuleResolver,
        ISalesRepOrderSortRuleResolver sortRuleResolver)
        : base(organizationAccessService)
    {
        _customerOrderSearchService = customerOrderSearchService;
        _responseGroupParser = responseGroupParser;
        _filterRuleResolver = filterRuleResolver;
        _sortRuleResolver = sortRuleResolver;
    }

    public virtual async Task<SalesRepOrderSearchResult> Handle(SalesRepOrdersQuery request, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<SalesRepOrderSearchResult>.TryCreateInstance();

        if (string.IsNullOrEmpty(request.UserId))
        {
            return result;
        }

        var organizationIds = await GetVisibleOrganizationIdsAsync(request.UserId, request.OrganizationId);
        if (organizationIds.Count == 0)
        {
            return result;
        }

        var criteria = BuildSearchCriteria(request, organizationIds);

        criteria = await _sortRuleResolver.ApplySortAsync(request.StoreId, request.Sort, criteria);

        var filteredCriteria = await _filterRuleResolver.ApplyListFilterAsync(request.StoreId, request.Filter, criteria);
        if (filteredCriteria == null)
        {
            return result;
        }

        var searchResult = await _customerOrderSearchService.SearchAsync(filteredCriteria);

        result.TotalCount = searchResult.TotalCount;
        result.Results = searchResult.Results
            .Select(SalesRepOrder.FromOrder)
            .ToList();

        return result;
    }

    protected virtual CustomerOrderSearchCriteria BuildSearchCriteria(SalesRepOrdersQuery request, IList<string> organizationIds)
    {
        var criteria = request.GetSearchCriteria<CustomerOrderSearchCriteria>();
        criteria.OrganizationIds = organizationIds.ToArray();
        criteria.CustomerId = request.UserId;
        criteria.StoreIds = string.IsNullOrEmpty(request.StoreId) ? null : [request.StoreId];
        criteria.ResponseGroup = _responseGroupParser.GetResponseGroup(request.IncludeFields);

        criteria.StartDate = request.Period?.From;
        criteria.EndDate = request.Period?.To;

        return criteria;
    }
}
