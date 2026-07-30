using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTopSellersQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepTopSellersQuery, IList<SalesRepTopSeller>>
{
    private readonly ISalesRepTopSellerService _topSellerService;
    private readonly ISalesRepTopSellerSortRuleResolver _sortRuleResolver;
    private readonly ISalesRepTopSellerFilterRuleResolver _filterRuleResolver;
    private readonly ISalesRepCurrencyResolver _currencyResolver;

    public SalesRepTopSellersQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepTopSellerService topSellerService,
        ISalesRepTopSellerSortRuleResolver sortRuleResolver,
        ISalesRepTopSellerFilterRuleResolver filterRuleResolver,
        ISalesRepCurrencyResolver currencyResolver)
        : base(organizationAccessService)
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

        var organizationIds = await OrganizationAccessService.GetVisibleOrganizationIdsAsync(request.UserId, request.OrganizationId);
        if (organizationIds.Count == 0)
        {
            return [];
        }

        var currencyCode = await _currencyResolver.ResolveCurrencyCodeAsync(request.CurrencyCode, request.StoreId);

        var criteria = AbstractTypeFactory<SalesRepTopSellerCriteria>.TryCreateInstance();
        criteria.OrganizationIds = organizationIds;
        criteria.CustomerId = request.UserId;
        criteria.StoreId = request.StoreId;
        criteria.CurrencyCode = currencyCode;
        criteria.FromDate = request.Period?.From;
        criteria.ToDate = request.Period?.To;
        criteria.Take = Math.Clamp(request.Take, 1, SalesRepTopSellersQuery.MaxTake);

        criteria = await _sortRuleResolver.ApplySortAsync(request.StoreId, request.Sort, criteria);

        var filteredCriteria = await _filterRuleResolver.ApplyListFilterAsync(request.StoreId, request.Filter, criteria);
        if (filteredCriteria == null)
        {
            return [];
        }

        return await _topSellerService.GetTopSellersAsync(filteredCriteria);
    }
}
