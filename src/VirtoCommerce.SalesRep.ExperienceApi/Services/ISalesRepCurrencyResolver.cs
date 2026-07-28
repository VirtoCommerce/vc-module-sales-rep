using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepCurrencyResolver
{
    Task<string> ResolveCurrencyCodeAsync(string requestedCurrencyCode, string storeId);
}
