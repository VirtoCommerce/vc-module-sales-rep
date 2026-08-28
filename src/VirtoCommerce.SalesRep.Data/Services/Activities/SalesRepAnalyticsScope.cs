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
    // The single construction site for scoped analytics reads: going through it is what makes it structurally
    // impossible for a new reader to forget the scope filters.
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

    // Every analytics read is scoped server-side: only the customer's own sessions (never impersonated ones)
    // and only the organizations the rep serves.
    public static IList<AnalyticsDimensionFilter> CreateScopeFilters(IList<string> organizationIds)
    {
        return [CreateSelfSessionFilter(), CreateOrganizationFilter(organizationIds)];
    }

    public static AnalyticsDimensionFilter CreateSelfSessionFilter()
    {
        var result = AbstractTypeFactory<AnalyticsDimensionFilter>.TryCreateInstance();

        result.DimensionName = AnalyticsConstants.UserDimensions.SessionKind;
        result.Values = [ModuleConstants.Analytics.SessionKinds.Self];

        return result;
    }

    public static AnalyticsDimensionFilter CreateOrganizationFilter(IList<string> organizationIds)
    {
        var result = AbstractTypeFactory<AnalyticsDimensionFilter>.TryCreateInstance();

        result.DimensionName = AnalyticsConstants.UserDimensions.OrganizationId;
        result.Values = organizationIds.ToList();

        return result;
    }

    public static string GetDimension(AnalyticsEvent analyticsEvent, string dimensionName)
    {
        return analyticsEvent.Dimensions?.TryGetValue(dimensionName, out var value) == true ? value : null;
    }
}
