using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepProductResolver
{
    Task<IDictionary<string, SalesRepActivityProduct>> ResolveByCodesAsync(IList<string> codes, string storeId, string cultureName);
}
