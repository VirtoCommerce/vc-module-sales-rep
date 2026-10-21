using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public class SalesRepAnalyticsScope : ISalesRepAnalyticsScope
{
    // The summary read has its own criteria type and takes CreateScopeFilters.
    public virtual AnalyticsEventSearchCriteria CreateCriteria(
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

    public virtual IList<AnalyticsDimensionFilter> CreateScopeFilters(IList<string> organizationIds)
    {
        return [.. CreateAudienceFilters(), CreateOrganizationFilter(organizationIds)];
    }

    public virtual IList<AnalyticsDimensionFilter> CreateAudienceFilters()
    {
        return
        [
            CreateFilter(AnalyticsConstants.UserDimensions.SessionKind, [ModuleConstants.Analytics.SessionKinds.Self]),
            CreateFilter(AnalyticsConstants.UserDimensions.IsSalesRep, [ModuleConstants.Analytics.IsSalesRepValues.Customer]),
        ];
    }

    protected virtual AnalyticsDimensionFilter CreateOrganizationFilter(IList<string> organizationIds)
    {
        // Refused here, not only downstream: an organization-less read is scoped by session kind alone — every
        // organization.
        if (organizationIds.IsNullOrEmpty())
        {
            throw new ArgumentException("An analytics read must name at least one organization.", nameof(organizationIds));
        }

        return CreateFilter(AnalyticsConstants.UserDimensions.OrganizationId, organizationIds.ToList());
    }

    protected static AnalyticsDimensionFilter CreateFilter(string dimensionName, IList<string> values)
    {
        var result = AbstractTypeFactory<AnalyticsDimensionFilter>.TryCreateInstance();

        result.DimensionName = dimensionName;
        result.Values = values;

        return result;
    }
}
