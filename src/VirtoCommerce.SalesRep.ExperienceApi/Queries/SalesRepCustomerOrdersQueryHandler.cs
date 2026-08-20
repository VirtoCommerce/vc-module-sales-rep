using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.OrdersModule.Core.Search.Indexed;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XOrder.Core.Queries;
using VirtoCommerce.XOrder.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerOrdersQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomerOrdersQuery, SearchOrderResponse>
{
    private const string DefaultSort = "createdDate:desc";

    private readonly IIndexedCustomerOrderSearchService _indexedOrderSearchService;
    private readonly ICustomerOrderAggregateRepository _orderAggregateRepository;
    private readonly IMapper _mapper;

    public SalesRepCustomerOrdersQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IIndexedCustomerOrderSearchService indexedOrderSearchService,
        ICustomerOrderAggregateRepository orderAggregateRepository,
        IMapper mapper)
        : base(organizationAccessService)
    {
        _indexedOrderSearchService = indexedOrderSearchService;
        _orderAggregateRepository = orderAggregateRepository;
        _mapper = mapper;
    }

    public virtual async Task<SearchOrderResponse> Handle(SalesRepCustomerOrdersQuery request, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<SearchOrderResponse>.TryCreateInstance();
        result.Results = [];
        result.Facets = [];

        if (string.IsNullOrEmpty(request.UserId))
        {
            return result;
        }

        var organizationIds = await OrganizationAccessService.GetVisibleOrganizationIdsAsync(request.UserId, request.OrganizationId);
        if (organizationIds.Count == 0)
        {
            return result;
        }

        var criteria = BuildSearchCriteria(request, organizationIds);

        var searchResult = await _indexedOrderSearchService.SearchCustomerOrdersAsync(criteria);

        result.TotalCount = searchResult.TotalCount;
        result.Results = await _orderAggregateRepository.GetAggregatesFromOrdersAsync(searchResult.Results, request.CultureName);
        result.Facets = ConvertFacets(searchResult.Aggregations, request.CultureName);

        return result;
    }

    protected virtual CustomerOrderIndexedSearchCriteria BuildSearchCriteria(SalesRepCustomerOrdersQuery request, IList<string> organizationIds)
    {
        var criteria = request.GetSearchCriteria<CustomerOrderIndexedSearchCriteria>();

        // The served organizations are the security boundary. `organizationId` only narrows that set, and the
        // caller's phrase filter is ANDed with this term filter, so a phrase naming another organization can
        // only intersect to nothing — it can never widen the scope.
        criteria.OrganizationIds = organizationIds.ToArray();
        criteria.StoreIds = string.IsNullOrEmpty(request.StoreId) ? null : [request.StoreId];
        criteria.LanguageCode = request.CultureName;
        criteria.Keyword = request.Filter;
        criteria.Facet = request.Facet;
        criteria.Sort = request.Sort.EmptyToNull() ?? DefaultSort;

        return criteria;
    }

    protected virtual IList<FacetResult> ConvertFacets(IList<OrderAggregation> aggregations, string cultureName)
    {
        return aggregations?
            .Select(x => _mapper.Map<FacetResult>(x, options => options.Items["cultureName"] = cultureName))
            .ToList() ?? [];
    }
}
