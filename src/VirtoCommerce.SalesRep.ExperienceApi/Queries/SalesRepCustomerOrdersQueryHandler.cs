using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.OrdersModule.Core.Search.Indexed;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XOrder.Core.Queries;
using VirtoCommerce.XOrder.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerOrdersQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomerOrdersQuery, SearchOrderResponse>
{
    private const string DefaultSort = "createdDate:desc";

    private static readonly Dictionary<string, string> _allowedFacets =
        ModuleConstants.OrderFacets.All.ToDictionary(x => x, StringComparer.OrdinalIgnoreCase);

    private readonly IIndexedCustomerOrderSearchService _indexedOrderSearchService;
    private readonly ICustomerOrderAggregateRepository _orderAggregateRepository;
    private readonly ISalesRepCustomerOrderResponseGroupParser _responseGroupParser;
    private readonly ISalesRepMapper _mapper;

    public SalesRepCustomerOrdersQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IIndexedCustomerOrderSearchService indexedOrderSearchService,
        ICustomerOrderAggregateRepository orderAggregateRepository,
        ISalesRepCustomerOrderResponseGroupParser responseGroupParser,
        ISalesRepMapper mapper)
        : base(organizationAccessService)
    {
        _indexedOrderSearchService = indexedOrderSearchService;
        _orderAggregateRepository = orderAggregateRepository;
        _responseGroupParser = responseGroupParser;
        _mapper = mapper;
    }

    public virtual async Task<SearchOrderResponse> Handle(SalesRepCustomerOrdersQuery request, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<SearchOrderResponse>.TryCreateInstance();
        result.Results = [];
        result.Facets = [];

        // Both are replaced wholesale on the success path; they exist so an early return still answers with
        // empty collections rather than nulls the connection would have to guard.
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
        result.Facets = _mapper.ToFacets(searchResult.Aggregations, request.CultureName);

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
        criteria.ResponseGroup = _responseGroupParser.GetResponseGroup(request.IncludeFields);
        criteria.Keyword = request.Filter;
        criteria.Facet = SanitizeFacet(request.Facet);
        criteria.Sort = request.Sort.EmptyToNull() ?? DefaultSort;

        return criteria;
    }

    /// <summary>
    /// IMPORTANT: do not widen this whitelist without re-reading why it exists.
    /// ApplyMultiSelectFacetSearch ANDs the whole search filter onto every aggregation, minus the child filters
    /// whose field name the aggregated field name *starts with* — that is what lets a facet count the buckets
    /// its own selection excludes. The rep's scope rides in that same filter, as a term filter on
    /// "organizationid" (plus "storeid" when the caller supplies one), so aggregating a field whose name begins
    /// with a scoping field's name strips the scope and counts across the entire index. "status" and
    /// "organizationname" begin with neither, which is precisely why they are safe to offer.
    /// A candidate field qualifies only if no scoping filter's field name is a prefix of it.
    /// </summary>
    protected virtual string SanitizeFacet(string facet)
    {
        if (string.IsNullOrEmpty(facet))
        {
            return facet;
        }

        // Answer in the module's own spelling rather than the caller's: the field name comes back as the facet
        // name, and both providers match it case-insensitively, so echoing "STATUS" would only mean the same
        // facet arrives under a different name each time it is asked for.
        var fields = facet
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => _allowedFacets.GetValueOrDefault(x))
            .Where(x => x != null)
            .ToArray();

        return fields.Length == 0 ? null : string.Join(' ', fields);
    }
}
