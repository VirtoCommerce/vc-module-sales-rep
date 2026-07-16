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

        // Resolve the selected statuses to their underlying order statuses (1:many for composite/overridden statuses).
        // If they were provided but resolve to nothing (all unrecognized for this store), no order can match — return
        // an empty result rather than silently dropping the filter and returning every order.
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

    /// <summary>
    /// Assembles the order search criteria for the given organizations and already-resolved statuses. Override to
    /// customize the criteria — e.g. extra filters, response group or sort. Security scoping (which organizations)
    /// and status resolution happen in <see cref="Handle"/>; this method only shapes the criteria.
    /// </summary>
    protected virtual CustomerOrderSearchCriteria BuildSearchCriteria(SalesRepOrdersQuery request, string[] organizationIds, string[] statuses)
    {
        // Keyword/Sort/Skip/Take come from the SearchQuery base; set only the order-specific bits here.
        var criteria = request.GetSearchCriteria<CustomerOrderSearchCriteria>();
        criteria.OrganizationIds = organizationIds;
        // Only orders created by this sales rep — their user id is the order's CustomerId (exactly as X-Order scopes
        // its "my orders" list: CanAccessOrderAuthorizationHandler sets SearchCustomerOrderQuery.CustomerId = current
        // user id). Combined with the org scoping, the list is the rep's own orders for the customer(s), not every
        // order of the organization.
        criteria.CustomerId = request.UserId;
        // Scope to the caller's store when provided so a rep never sees another store's orders.
        criteria.StoreIds = string.IsNullOrEmpty(request.StoreId) ? null : [request.StoreId];
        // Load only the order data the caller actually selected (e.g. skip line items when itemsCount isn't asked for).
        criteria.ResponseGroup = _responseGroupParser.GetResponseGroup(request.IncludeFields);

        // Apply the resolved status filter when present (null/empty = the caller didn't filter by status).
        if (statuses?.Length > 0)
        {
            criteria.Statuses = statuses;
        }

        // Recent orders on top by default (VCST-5308); an explicit sort argument overrides it.
        if (string.IsNullOrEmpty(criteria.Sort))
        {
            criteria.Sort = "createdDate:desc";
        }

        return criteria;
    }

    /// <summary>
    /// The organization ids whose orders the caller may see: a single requested customer the rep serves, or — when
    /// no customer is specified — every organization the rep is assigned to (the cross-customer dashboard). Returns
    /// an empty array when the rep serves none (or doesn't serve the requested one), so the caller returns no orders.
    /// </summary>
    protected virtual async Task<string[]> GetVisibleOrganizationIdsAsync(SalesRepOrdersQuery request)
    {
        if (!string.IsNullOrEmpty(request.OrganizationId))
        {
            // Single customer: the caller must hold an active granting membership in exactly this organization,
            // else a rep could read any organization's orders by guessing its id.
            var memberships = await GetGrantingMembershipsAsync([request.UserId], [request.OrganizationId]);
            return memberships.Count > 0 ? [request.OrganizationId] : [];
        }

        // Dashboard: every organization the rep is assigned to (passed as a single array filter to the search).
        return await GetServedOrganizationIdsAsync(request.UserId);
    }
}
