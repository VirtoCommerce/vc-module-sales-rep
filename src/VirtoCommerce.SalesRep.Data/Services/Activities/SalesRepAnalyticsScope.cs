using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public static class SalesRepAnalyticsScope
{
    // Every analytics read is scoped server-side: only the customer's own sessions (never impersonated ones)
    // and only the organizations the rep serves.
    public static IList<AnalyticsDimensionFilter> CreateScopeFilters(IList<string> organizationIds)
    {
        var sessionKindFilter = AbstractTypeFactory<AnalyticsDimensionFilter>.TryCreateInstance();
        sessionKindFilter.DimensionName = ModuleConstants.UserDimensions.SessionKind;
        sessionKindFilter.Values = [ModuleConstants.SessionKinds.Self];

        var organizationFilter = AbstractTypeFactory<AnalyticsDimensionFilter>.TryCreateInstance();
        organizationFilter.DimensionName = ModuleConstants.UserDimensions.OrganizationId;
        organizationFilter.Values = organizationIds.ToList();

        return [sessionKindFilter, organizationFilter];
    }
}
