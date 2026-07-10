using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Loads a page of one customer organization's orders for the Sales Rep customer profile (VCST-5308). Mirrors the
/// storefront orders search (keyword/sort/paging) but goes straight through the Orders module's public
/// <see cref="ICustomerOrderSearchService"/> — this module stays independent of X-Order and its GraphQL types.
/// </summary>
public class SalesRepOrdersQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepOrdersQuery, SalesRepOrderSearchResult>
{
    // WithItems populates the line-item count; WithPrices keeps the grand total (the order pipeline zeroes it for
    // lighter response groups).
    private static readonly string _responseGroup =
        (CustomerOrderResponseGroup.WithItems | CustomerOrderResponseGroup.WithPrices).ToString();

    private readonly ICustomerOrderSearchService _customerOrderSearchService;

    public SalesRepOrdersQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ICustomerOrderSearchService customerOrderSearchService)
        : base(roleResolver, membershipSearchService)
    {
        _customerOrderSearchService = customerOrderSearchService;
    }

    public virtual async Task<SalesRepOrderSearchResult> Handle(SalesRepOrdersQuery request, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<SalesRepOrderSearchResult>.TryCreateInstance();

        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.CustomerId))
        {
            return result;
        }

        // Security scoping: the caller must hold an active sales-rep-granting membership in exactly the requested
        // organization. Without this a rep could read any organization's orders by guessing its id.
        // OnlyUnlocked: a rep locked in an organization must not see it as a customer.
        var memberships = await GetGrantingMembershipsAsync(
            [request.UserId],
            [request.CustomerId]);

        if (memberships.Count == 0)
        {
            return result;
        }

        var criteria = AbstractTypeFactory<CustomerOrderSearchCriteria>.TryCreateInstance();
        criteria.OrganizationIds = [request.CustomerId];
        // Scope to the caller's store when provided so a rep never sees another store's orders.
        criteria.StoreIds = string.IsNullOrEmpty(request.StoreId) ? null : [request.StoreId];
        criteria.ResponseGroup = _responseGroup;
        criteria.Keyword = request.Keyword;
        // Recent orders on top by default (VCST-5308); an explicit sort argument overrides it.
        criteria.Sort = string.IsNullOrEmpty(request.Sort) ? "createdDate:desc" : request.Sort;
        criteria.Skip = request.Skip;
        criteria.Take = request.Take;

        var searchResult = await _customerOrderSearchService.SearchAsync(criteria);

        result.TotalCount = searchResult.TotalCount;
        result.Results = searchResult.Results
            .Select(SalesRepOrder.FromOrder)
            .ToList();

        return result;
    }
}
