using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepActivitiesQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepActivitiesQuery, SalesRepActivitySearchResult>
{
    private readonly ISalesRepActivityService _activityService;
    private readonly ISalesRepProductResolver _productResolver;

    public SalesRepActivitiesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepActivityService activityService,
        ISalesRepProductResolver productResolver)
        : base(organizationAccessService)
    {
        _activityService = activityService;
        _productResolver = productResolver;
    }

    public virtual async Task<SalesRepActivitySearchResult> Handle(SalesRepActivitiesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId))
        {
            return null;
        }

        var organizationIds = await OrganizationAccessService.GetVisibleOrganizationIdsAsync(request.UserId, request.OrganizationId);
        if (organizationIds.Count == 0)
        {
            return null;
        }

        var criteria = AbstractTypeFactory<SalesRepActivitySearchCriteria>.TryCreateInstance();
        criteria.SalesRepUserId = request.UserId;
        criteria.OrganizationIds = organizationIds;
        criteria.Categories = request.Categories;
        criteria.StoreId = request.StoreId;
        criteria.From = request.Period?.From;
        criteria.To = request.Period?.To;
        criteria.Take = Math.Clamp(request.Take, 0, SalesRepActivitiesQuery.MaxTake);
        criteria.Skip = Math.Clamp(request.Skip, 0, ModuleConstants.Activities.MaxSkip);

        var result = await _activityService.SearchActivitiesAsync(criteria);

        await ResolveProductsAsync(result, request);

        return result;
    }

    protected virtual async Task ResolveProductsAsync(SalesRepActivitySearchResult result, SalesRepActivitiesQuery request)
    {
        var productViewRows = result.Results
            .Where(x => x.Category == ModuleConstants.Activities.Categories.ProductViews && !string.IsNullOrEmpty(x.ProductCode))
            .ToList();
        if (productViewRows.Count == 0)
        {
            return;
        }

        var codes = productViewRows.Select(x => x.ProductCode).ToList();
        var productsByCode = await _productResolver.ResolveByCodesAsync(codes);

        foreach (var row in productViewRows)
        {
            if (!productsByCode.TryGetValue(row.ProductCode, out var product))
            {
                continue;
            }

            row.ProductId = product.ProductId;
            row.ProductImageUrl = product.ImageUrl;

            if (!string.IsNullOrEmpty(product.Name))
            {
                row.ProductName = product.Name;
            }
        }
    }
}
