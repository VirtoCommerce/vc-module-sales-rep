using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Resolves the currency a Sales Rep money figure is expressed in, applying the one defaulting order shared by every
/// Sales Rep dashboard query (statistics, customers list, top sellers): the client's requested code wins; otherwise
/// the store's default currency; otherwise the platform primary currency. Centralized so that, for a given store,
/// every widget on the dashboard folds and displays money in the same currency. A project overrides this service
/// (DI last-registration wins) to change the policy.
/// </summary>
public interface ISalesRepCurrencyResolver
{
    /// <summary>
    /// Resolves the effective currency code: <paramref name="requestedCurrencyCode"/> if set; otherwise the
    /// <paramref name="storeId"/> store's default currency; otherwise the platform primary currency (null if none).
    /// </summary>
    Task<string> ResolveCurrencyCodeAsync(string requestedCurrencyCode, string storeId);
}
