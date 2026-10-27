using System;
using System.Collections.Generic;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

// The filters every rep-facing analytics read carries. Every reader takes them from here, so overriding this one
// service changes the scope of every read, the diagnostics probe included.
public interface ISalesRepAnalyticsScope
{
    // An event read with the scope filters on it.
    AnalyticsEventSearchCriteria CreateCriteria(
        string storeId,
        IList<string> organizationIds,
        IList<string> eventNames,
        IList<string> dimensionNames,
        DateTime? from,
        DateTime? to);

    // The audience filters plus the organizations the rep serves. Refuses an empty organization list.
    IList<AnalyticsDimensionFilter> CreateScopeFilters(IList<string> organizationIds);

    // Only customers' own sessions — never impersonated ones, never a rep's own.
    IList<AnalyticsDimensionFilter> CreateAudienceFilters();
}
