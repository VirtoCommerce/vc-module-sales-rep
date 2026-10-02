using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepProductResolver
{
    // Resolves every row's product code (GA itemId) in one batched search, scoped to the store's catalog. Without a
    // storeId, a code carried by several catalogs resolves to NOTHING, not to whichever came first. Rows whose code
    // is empty, unknown or ambiguous are left untouched, so their tracked values survive.
    Task ResolveAsync<T>(IList<T> rows, string storeId, Func<T, string> getCode, Action<T, SalesRepActivityProduct> setProduct);
}
