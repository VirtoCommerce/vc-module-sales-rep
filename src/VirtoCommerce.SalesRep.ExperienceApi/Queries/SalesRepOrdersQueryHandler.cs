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
/// stays independent of X-Order and its GraphQL types. Ordering is a named sort rule (the <c>sort</c> argument is a
/// <c>salesRepOrderSortRules</c> name), resolved to the search criteria's sort by the sort-rule resolver.
/// </summary>
public class SalesRepOrdersQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepOrdersQuery, SalesRepOrderSearchResult>
{
    private readonly ISalesRepCustomerOrderSearchService _customerOrderSearchService;
    private readonly ISalesRepOrderResponseGroupParser _responseGroupParser;
    private readonly ISalesRepOrderFilterRuleResolver _filterRuleResolver;
    private readonly ISalesRepOrderSortRuleResolver _sortRuleResolver;

    public SalesRepOrdersQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepCustomerOrderSearchService customerOrderSearchService,
        ISalesRepOrderResponseGroupParser responseGroupParser,
        ISalesRepOrderFilterRuleResolver filterRuleResolver,
        ISalesRepOrderSortRuleResolver sortRuleResolver)
        : base(roleResolver, membershipSearchService)
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

        // Which organizations' orders the caller may see: the one requested customer (only if the rep serves it),
        // or — when no customer is specified — every organization the rep is assigned to (the dashboard).
        var organizationIds = await GetVisibleOrganizationIdsAsync(request);
        if (organizationIds.Length == 0)
        {
            return result;
        }

        var criteria = BuildSearchCriteria(request, organizationIds);

        // Resolve the selected ordering (the sort argument is a salesRepOrderSortRules 'name'); empty/unknown → the
        // default ordering (a sort only reorders, so it never fails closed).
        criteria = await _sortRuleResolver.ApplySortAsync(request.StoreId, request.Sort, criteria);

        // Apply the selected rule through the SAME resolver the order statistics use (so the list and the stats
        // filter identically). Null means a rule name was given but is unrecognized — return an empty result rather
        // than silently dropping the filter and returning every order. No concrete filter field is inspected here.
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

    /// <summary>
    /// Assembles the security-scoped, paged order search criteria for the given organizations (no status filter and
    /// no sort — those are applied afterwards in <see cref="Handle"/> via the shared filter- and sort-rule resolvers).
    /// Override to customize the criteria — e.g. extra filters or response group.
    /// </summary>
    protected virtual CustomerOrderSearchCriteria BuildSearchCriteria(SalesRepOrdersQuery request, string[] organizationIds)
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

        // Optional created-date range. "recent" ordering ignores it; date-scoped views (e.g. "biggest orders this
        // quarter") pass it.
        criteria.StartDate = request.Period?.From;
        criteria.EndDate = request.Period?.To;

        return criteria;
    }

    /// <summary>
    /// The organization ids whose orders the caller may see: a single requested customer the rep serves, or — when
    /// no customer is specified — every organization the rep is assigned to (the cross-customer dashboard). Returns
    /// an empty array when the rep serves none (or doesn't serve the requested one), so the caller returns no orders.
    /// </summary>
    protected virtual Task<string[]> GetVisibleOrganizationIdsAsync(SalesRepOrdersQuery request)
        => GetVisibleOrganizationIdsAsync(request.UserId, request.OrganizationId);
}
