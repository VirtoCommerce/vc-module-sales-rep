using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepActivitiesQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepActivitiesQuery, SalesRepActivitySearchResult>
{
    private readonly ISalesRepActivityService _activityService;
    private readonly ISalesRepProductResolver _productResolver;
    private readonly ISalesRepAnalyticsAvailability _availability;
    private readonly ISalesRepStoreAccessService _storeAccessService;

    public SalesRepActivitiesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepActivityService activityService,
        ISalesRepProductResolver productResolver,
        ISalesRepAnalyticsAvailability availability,
        ISalesRepStoreAccessService storeAccessService)
        : base(organizationAccessService)
    {
        _activityService = activityService;
        _productResolver = productResolver;
        _availability = availability;
        _storeAccessService = storeAccessService;
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

        if (!await _storeAccessService.IsAllowedAsync(request.UserId, request.StoreId))
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
        criteria.Skip = Math.Max(request.Skip, 0);
        criteria.IncludeCategoryCounts = request.IncludeFields.IncludesField(nameof(SalesRepActivitySearchResult.CategoryCounts));

        // Past the paging window: no rows, not a clamped Skip — page 40 would repeat the window's last page, which a
        // caller cannot tell from real data. The counters still report the whole set.
        if (criteria.Skip > ModuleConstants.Activities.MaxSkip)
        {
            criteria.Take = 0;
        }

        var result = await _activityService.SearchActivitiesAsync(criteria);
        // AND, not assignment: a source that caught a failed read has already turned this off.
        result.IsAnalyticsAvailable = result.IsAnalyticsAvailable && await _availability.IsConfiguredAsync(criteria.StoreId);

        await ResolveProductsAsync(result, request);

        return result;
    }

    protected virtual Task ResolveProductsAsync(SalesRepActivitySearchResult result, SalesRepActivitiesQuery request)
    {
        var productViewRows = result.Results
            .Where(x => x.Category == ModuleConstants.Activities.Categories.ProductViews)
            .ToList();

        return _productResolver.ResolveAsync(productViewRows, request.StoreId, x => x.ProductCode, (row, product) =>
        {
            row.ProductId = product.ProductId;
            row.ProductImageUrl = product.ImageUrl;
            row.ProductName = product.Name.EmptyToNull() ?? row.ProductName;
        });
    }
}
