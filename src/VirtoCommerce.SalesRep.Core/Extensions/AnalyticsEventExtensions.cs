using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Extensions;

public static class AnalyticsEventExtensions
{
    public static string GetDimension(this AnalyticsEvent analyticsEvent, string dimensionName)
    {
        return analyticsEvent.Dimensions?.GetValueSafe(dimensionName);
    }
}
