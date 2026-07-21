using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Ranks the rep's top-selling products (VCST-5309). Scopes to the organizations the rep may see (the requested one
/// if served, else all assigned) and to the rep's own orders (creator scope — the data-isolation invariant), then
/// applies the selected ordering and optional category badge and returns the top-N via
/// <see cref="ISalesRepTopSellerService"/>.
/// </summary>
public class SalesRepTopSellersQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepTopSellersQuery, IList<SalesRepTopSeller>>
{
    private readonly ISalesRepTopSellerService _topSellerService;
    private readonly ISalesRepTopSellerSortRuleResolver _sortRuleResolver;
    private readonly ISalesRepTopSellerFilterRuleResolver _filterRuleResolver;
    private readonly ISalesRepCurrencyResolver _currencyResolver;

    public SalesRepTopSellersQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepTopSellerService topSellerService,
        ISalesRepTopSellerSortRuleResolver sortRuleResolver,
        ISalesRepTopSellerFilterRuleResolver filterRuleResolver,
        ISalesRepCurrencyResolver currencyResolver)
        : base(roleResolver, membershipSearchService)
    {
        _topSellerService = topSellerService;
        _sortRuleResolver = sortRuleResolver;
        _filterRuleResolver = filterRuleResolver;
        _currencyResolver = currencyResolver;
    }

    public virtual async Task<IList<SalesRepTopSeller>> Handle(SalesRepTopSellersQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId))
        {
            return [];
        }

        var organizationIds = await GetVisibleOrganizationIdsAsync(request.UserId, request.OrganizationId);
        if (organizationIds.Length == 0)
        {
            return [];
        }

        // Client currency wins; else the store's default; else the platform primary.
        var currencyCode = await _currencyResolver.ResolveCurrencyCodeAsync(request.CurrencyCode, request.StoreId);

        var criteria = AbstractTypeFactory<SalesRepTopSellerCriteria>.TryCreateInstance();
        criteria.OrganizationIds = organizationIds;
        criteria.CustomerId = request.UserId; // creator scope (data-isolation invariant)
        criteria.StoreId = request.StoreId;
        criteria.CurrencyCode = currencyCode;
        criteria.FromDate = request.Period?.From;
        criteria.ToDate = request.Period?.To;
        criteria.Take = Math.Clamp(request.Take, 1, SalesRepTopSellersQuery.MaxTake);

        // Ordering (empty/unknown → default by-units; a sort never fails closed).
        criteria = await _sortRuleResolver.ApplySortAsync(request.StoreId, request.Sort, criteria);

        // Category badge (empty → all categories; unrecognized → fail-closed, no results).
        var filteredCriteria = await _filterRuleResolver.ApplyListFilterAsync(request.StoreId, request.Filter, criteria);
        if (filteredCriteria == null)
        {
            return [];
        }

        return await _topSellerService.GetTopSellersAsync(filteredCriteria);
    }
}
