using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.Core.Services;

// Installed AND configured for the store: one answer for every surface, so "not measured" never reads as
// "the customer was quiet".
public interface ISalesRepAnalyticsAvailability
{
    Task<bool> IsConfiguredAsync(string storeId);
}
