using System.Collections.Generic;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.SalesRep.Data.Services.Activities;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// A project's override of the analytics scope: one more audience filter, added in the one place every read takes
/// its scope from.
/// </summary>
internal sealed class ChannelAnalyticsScope : SalesRepAnalyticsScope
{
    public const string ChannelDimension = "channel";
    public const string WebChannel = "web";

    public override IList<AnalyticsDimensionFilter> CreateAudienceFilters()
    {
        return [.. base.CreateAudienceFilters(), CreateFilter(ChannelDimension, [WebChannel])];
    }
}
