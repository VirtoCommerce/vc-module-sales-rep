using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// Whether tracked storefront activity can be read for a store at all — the analytics module installed AND
/// configured for it. One answer for every surface that shows tracked figures: without it "not measured" and
/// "the customer was quiet" are the same empty screen, and a rep reads the second when the truth is the first.
/// </summary>
public interface ISalesRepAnalyticsAvailability
{
    Task<bool> IsConfiguredAsync(string storeId);
}
