using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Loads a page of one customer organization's orders for the Sales Rep customer profile (VCST-5308). Mirrors the
/// storefront orders search (keyword/sort/paging) but goes straight through the Orders module's public
/// <see cref="ICustomerOrderSearchService"/> — this module stays independent of X-Order and its GraphQL types.
/// </summary>
public class SalesRepOrdersQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepOrdersQuery, SalesRepOrderSearchResult>
{
    private readonly ICustomerOrderSearchService _customerOrderSearchService;
    private readonly ISalesRepOrderResponseGroupParser _responseGroupParser;
    private readonly ISalesRepOrderStatusService _statusService;

    public SalesRepOrdersQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ICustomerOrderSearchService customerOrderSearchService,
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

        // Keyword/Sort/Skip/Take come from the SearchQuery base; set only the order-specific bits here.
        var criteria = request.GetSearchCriteria<CustomerOrderSearchCriteria>();
        criteria.OrganizationIds = [request.CustomerId];
        // Scope to the caller's store when provided so a rep never sees another store's orders.
        criteria.StoreIds = string.IsNullOrEmpty(request.StoreId) ? null : [request.StoreId];
        // Load only the order data the caller actually selected (e.g. skip line items when itemsCount isn't asked for).
        criteria.ResponseGroup = _responseGroupParser.GetResponseGroup(request.IncludeFields);
        // Selected statuses → the union of their underlying order statuses (1:many for composite/overridden
        // statuses). Filter only when something is selected and resolves to a non-empty set; otherwise all statuses.
        if (request.Statuses?.Count > 0)
        {
            var statuses = new List<string>();
            foreach (var selected in request.Statuses)
            {
                statuses.AddRange(await _statusService.ResolveOrderStatusesAsync(request.StoreId, selected));
            }

            var resolved = statuses.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
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

        await ApplyLocalizedStatusesAsync(request, result.Results);

        return result;
    }

    /// <summary>
    /// Fills each order's <see cref="SalesRepOrder.StatusLocalized"/> from the status service's raw → localized map,
    /// but only when the caller selected that field (so the extra lookup is skipped otherwise — consistent with the
    /// field-driven response group). Post-search "apply to each record" step, mirroring the news module's handlers.
    /// </summary>
    protected virtual async Task ApplyLocalizedStatusesAsync(SalesRepOrdersQuery request, IList<SalesRepOrder> orders)
    {
        var requested = request.IncludeFields.Any(x =>
            x.Split('.')[^1].Equals(nameof(SalesRepOrder.StatusLocalized), StringComparison.OrdinalIgnoreCase));

        if (!requested || orders.Count == 0)
        {
            return;
        }

        var localizedByStatus = await _statusService.GetLocalizedStatusesAsync(request.StoreId, request.CultureName);

        foreach (var order in orders)
        {
            if (!string.IsNullOrEmpty(order.Status) && localizedByStatus.TryGetValue(order.Status, out var label))
            {
                order.StatusLocalized = label;
            }
        }
    }
}
