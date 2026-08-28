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
        criteria.Skip = Math.Max(request.Skip, 0);

        // Past the paging window the feed has nothing to serve, and says so by returning no rows. Clamping Skip
        // instead would answer page 40 with the window's last page, which a caller cannot tell from real data.
        // The counters are unaffected: they still report the whole set, so a client can see the feed is longer
        // than it can page.
        if (criteria.Skip > ModuleConstants.Activities.MaxSkip)
        {
            criteria.Take = 0;
        }

        var result = await _activityService.SearchActivitiesAsync(criteria);

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
