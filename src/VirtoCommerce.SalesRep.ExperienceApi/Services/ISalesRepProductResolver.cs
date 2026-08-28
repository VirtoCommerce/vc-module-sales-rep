using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepProductResolver
{
    // Codes are the product codes analytics tracked (GA itemId). The lookup is scoped to the store's catalog, so
    // the same code in another catalog cannot answer; an unknown storeId leaves the search catalog-wide.
    Task<IDictionary<string, SalesRepActivityProduct>> ResolveByCodesAsync(IList<string> codes, string storeId);

    // Resolves every row's code in one batched search and hands the resolved product back per row. A row whose
    // code is empty or resolves to nothing is left untouched, so the caller's tracked values survive.
    Task ResolveAsync<T>(IList<T> rows, string storeId, Func<T, string> getCode, Action<T, SalesRepActivityProduct> setProduct);
}
