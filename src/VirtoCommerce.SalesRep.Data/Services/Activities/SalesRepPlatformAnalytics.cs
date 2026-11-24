using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.AnalyticsModule.Core.Models;
using VirtoCommerce.AnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public class SalesRepPlatformAnalytics : ISalesRepPlatformAnalytics
{
    private readonly IOptionalDependency<IAnalyticsEventSearchService> _searchService;
    private readonly IStoreService _storeService;
    private readonly ISalesRepOrganizationAccessService _organizationAccessService;

    public SalesRepPlatformAnalytics(
        IOptionalDependency<IAnalyticsEventSearchService> searchService,
        IStoreService storeService,
        ISalesRepOrganizationAccessService organizationAccessService)
    {
        _searchService = searchService;
        _storeService = storeService;
        _organizationAccessService = organizationAccessService;
    }

    public virtual async Task<IAnalyticsEventSearchService> GetSearchServiceAsync(string storeId)
    {
        var searchService = _searchService.Value;
        if (searchService == null || string.IsNullOrEmpty(storeId))
        {
            return null;
        }

        var store = (await _storeService.GetAsync([storeId], responseGroup: null, clone: false)).FirstOrDefault();
        var source = store?.Settings?.GetValue<string>(ModuleConstants.Settings.General.TrackedActivitySource);

        return ModuleConstants.Analytics.TrackedActivitySources.Platform.EqualsIgnoreCase(source) && await searchService.IsAvailableAsync(storeId)
            ? searchService
            : null;
    }

    public virtual async Task<AnalyticsEventSearchCriteria> CreateCriteriaAsync(
        string storeId,
        IList<string> organizationIds,
        string eventName,
        DateTime? from,
        DateTime? to)
    {
        // Refused here, as the Google Analytics scope does: an organization-less read is every organization's.
        if (organizationIds.IsNullOrEmpty())
        {
            throw new ArgumentException("An analytics read must name at least one organization.", nameof(organizationIds));
        }

        // A rep is a member of every organization they serve, so the rep's own storefront sessions carry that
        // organization like a customer's.
        var repMemberships = await _organizationAccessService.GetGrantingMembershipsAsync(organizationIds: organizationIds);

        var result = AbstractTypeFactory<AnalyticsEventSearchCriteria>.TryCreateInstance();

        result.StoreId = storeId;
        result.OrganizationIds = organizationIds.ToList();
        result.Names = [eventName];
        result.SessionKinds = [ModuleConstants.Analytics.SessionKinds.Self];
        result.ExcludedUserIds = repMemberships.Select(x => x.UserId).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        result.From = from;
        result.To = to;

        return result;
    }
}
