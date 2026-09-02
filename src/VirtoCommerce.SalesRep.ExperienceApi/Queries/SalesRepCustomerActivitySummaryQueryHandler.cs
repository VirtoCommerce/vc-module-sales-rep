using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerActivitySummaryQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomerActivitySummaryQuery, SalesRepCustomerActivitySummary>
{
    private readonly ISalesRepCustomerActivityService _customerActivityService;
    private readonly IMemberService _memberService;
    private readonly ISalesRepProductResolver _productResolver;
    private readonly ISalesRepStoreAccessService _storeAccessService;

    public SalesRepCustomerActivitySummaryQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepCustomerActivityService customerActivityService,
        IMemberService memberService,
        ISalesRepProductResolver productResolver,
        ISalesRepStoreAccessService storeAccessService)
        : base(organizationAccessService)
    {
        _customerActivityService = customerActivityService;
        _memberService = memberService;
        _productResolver = productResolver;
        _storeAccessService = storeAccessService;
    }

    public virtual async Task<SalesRepCustomerActivitySummary> Handle(SalesRepCustomerActivitySummaryQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.OrganizationId))
        {
            return null;
        }

        if (!await OrganizationAccessService.ServesOrganizationAsync(request.UserId, request.OrganizationId))
        {
            return null;
        }

        // A named store is a claim, not a filter: it chooses whose analytics property is read and whose
        // orders are counted, so it is checked against the caller's own store before it is used.
        if (!await _storeAccessService.IsAllowedAsync(request.UserId, request.StoreId))
        {
            return null;
        }


        var criteria = AbstractTypeFactory<SalesRepCustomerActivityCriteria>.TryCreateInstance();
        criteria.OrganizationId = request.OrganizationId;
        criteria.StoreId = request.StoreId;
        criteria.From = request.Period?.From;
        criteria.To = request.Period?.To;

        var result = await _customerActivityService.GetSummaryAsync(criteria);

        var organization = (await _memberService.GetByIdsAsync([request.OrganizationId], nameof(MemberResponseGroup.Default), [nameof(Organization)])).FirstOrDefault();
        result.CreatedOn = organization?.CreatedDate;

        await ResolveLastViewedProductAsync(result, request);

        return result;
    }

    protected virtual Task ResolveLastViewedProductAsync(SalesRepCustomerActivitySummary result, SalesRepCustomerActivitySummaryQuery request)
    {
        SalesRepActivityProduct[] rows = result.LastViewedProduct == null ? [] : [result.LastViewedProduct];

        return _productResolver.ResolveAsync(rows, request.StoreId, x => x.Code, (row, product) =>
        {
            row.ProductId = product.ProductId;
            row.ImageUrl = product.ImageUrl;
            row.Name = product.Name.EmptyToNull() ?? row.Name;
        });
    }
}
