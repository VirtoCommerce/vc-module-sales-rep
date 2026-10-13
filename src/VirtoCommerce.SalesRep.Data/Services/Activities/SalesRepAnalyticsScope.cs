using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public static class SalesRepAnalyticsScope
{
    // Scoped event reads are built here, so a new reader cannot forget the scope filters; the summary read has its own
    // criteria type and takes CreateScopeFilters.
    public static AnalyticsEventSearchCriteria CreateCriteria(
        string storeId,
        IList<string> organizationIds,
        IList<string> eventNames,
        IList<string> dimensionNames,
        DateTime? from,
        DateTime? to)
    {
        var result = AbstractTypeFactory<AnalyticsEventSearchCriteria>.TryCreateInstance();

        result.StoreId = storeId;
        result.EventNames = eventNames;
        result.DimensionNames = dimensionNames;
        result.DimensionFilters = CreateScopeFilters(organizationIds);
        result.From = from;
        result.To = to;

        return result;
    }

    // Only the organizations the rep serves, on top of the audience filters.
    public static IList<AnalyticsDimensionFilter> CreateScopeFilters(IList<string> organizationIds)
    {
        return [.. CreateAudienceFilters(), CreateOrganizationFilter(organizationIds)];
    }

    // Only customers' own sessions — never impersonated ones, never a rep's own.
    public static IList<AnalyticsDimensionFilter> CreateAudienceFilters()
    {
        return
        [
            CreateFilter(AnalyticsConstants.UserDimensions.SessionKind, [ModuleConstants.Analytics.SessionKinds.Self]),
            CreateFilter(AnalyticsConstants.UserDimensions.IsSalesRep, [ModuleConstants.Analytics.IsSalesRepValues.Customer]),
        ];
    }

    public static AnalyticsDimensionFilter CreateOrganizationFilter(IList<string> organizationIds)
    {
        // Refused here, not only downstream: an organization-less read is scoped by session kind alone — every
        // organization.
        if (organizationIds.IsNullOrEmpty())
        {
            throw new ArgumentException("An analytics read must name at least one organization.", nameof(organizationIds));
        }

        return CreateFilter(AnalyticsConstants.UserDimensions.OrganizationId, organizationIds.ToList());
    }

    public static string GetDimension(AnalyticsEvent analyticsEvent, string dimensionName)
    {
        return analyticsEvent.Dimensions?.GetValueSafe(dimensionName);
    }

    private static AnalyticsDimensionFilter CreateFilter(string dimensionName, IList<string> values)
    {
        var result = AbstractTypeFactory<AnalyticsDimensionFilter>.TryCreateInstance();

        result.DimensionName = dimensionName;
        result.Values = values;

        return result;
    }
}
