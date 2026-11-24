using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.AnalyticsModule.Core.Models;
using VirtoCommerce.AnalyticsModule.Core.Services;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// The hub's access to the platform's own analytics store, for a store that reads its tracked activity from there
/// instead of Google Analytics.
/// </summary>
public interface ISalesRepPlatformAnalytics
{
    /// <summary>
    /// Null unless the store chose the platform as its source and the store's events are really being kept: the
    /// caller then falls back to Google Analytics.
    /// </summary>
    Task<IAnalyticsEventSearchService> GetSearchServiceAsync(string storeId);

    /// <summary>
    /// Criteria scoped the way every hub read is: the given organizations, customers' own sessions only, and never
    /// the activity of the reps who serve those organizations.
    /// </summary>
    Task<AnalyticsEventSearchCriteria> CreateCriteriaAsync(string storeId, IList<string> organizationIds, string eventName, DateTime? from, DateTime? to);
}
