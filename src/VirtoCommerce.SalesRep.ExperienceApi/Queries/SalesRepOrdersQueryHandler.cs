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

/// <summary>
/// Loads a page of orders for the Sales Rep — a single customer organization (VCST-5308), or, when no customer is
/// specified, every organization the rep is assigned to (the cross-customer dashboard). Mirrors the storefront
/// orders search (keyword/sort/paging) but goes through the module's own
/// <see cref="ISalesRepCustomerOrderSearchService"/> (the Orders search service, subclassed) — so this module
/// stays independent of X-Order and its GraphQL types.
/// </summary>
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

        // Which organizations' orders the caller may see: the one requested customer (only if the rep serves it),
        // or — when no customer is specified — every organization the rep is assigned to (the dashboard).
        var organizationIds = await GetVisibleOrganizationIdsAsync(request);
        if (organizationIds.Length == 0)
        {
            return result;
        }

        // Keyword/Sort/Skip/Take come from the SearchQuery base; set only the order-specific bits here.
        var criteria = request.GetSearchCriteria<CustomerOrderSearchCriteria>();
        criteria.OrganizationIds = organizationIds;
        // Scope to the caller's store when provided so a rep never sees another store's orders.
        criteria.StoreIds = string.IsNullOrEmpty(request.StoreId) ? null : [request.StoreId];
        // Load only the order data the caller actually selected (e.g. skip line items when itemsCount isn't asked for).
        criteria.ResponseGroup = _responseGroupParser.GetResponseGroup(request.IncludeFields);
        // Selected statuses → the deduped union of their underlying order statuses (1:many for composite/overridden
        // statuses; resolved by the status service). Filter only when it resolves to a non-empty set.
        if (request.Statuses?.Count > 0)
        {
            var resolved = await _statusService.ResolveOrderStatusesAsync(request.StoreId, request.Statuses);
            if (resolved.Length > 0)
            {
                criteria.Statuses = resolved;
            }
        }
        // Recent orders on top by default (VCST-5308); an explicit sort argument overrides it.
        if (string.IsNullOrEmpty(criteria.Sort))
        {
            criteria.Sort = "createdDate:desc";
        }

        var searchResult = await _customerOrderSearchService.SearchAsync(criteria);

        result.TotalCount = searchResult.TotalCount;
        result.Results = searchResult.Results
            .Select(SalesRepOrder.FromOrder)
            .ToList();

        return result;
    }

    /// <summary>
    /// The organization ids whose orders the caller may see: a single requested customer the rep serves, or — when
    /// no customer is specified — every organization the rep is assigned to (the cross-customer dashboard). Returns
    /// an empty array when the rep serves none (or doesn't serve the requested one), so the caller returns no orders.
    /// </summary>
    protected virtual Task<string[]> GetVisibleOrganizationIdsAsync(SalesRepOrdersQuery request)
        => GetVisibleOrganizationIdsAsync(request.UserId, request.CustomerId);
}
