using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepProductResolver
{
    // Resolves every row's product code (the code analytics tracked as GA itemId) in one batched search and hands
    // the resolved product back per row. The lookup is scoped to the store's catalog, so the same code in another
    // catalog cannot answer. Without a storeId the search is catalog-wide and a code carried by more than one
    // catalog resolves to NOTHING rather than to whichever product came back first. A row whose code is empty,
    // unknown or ambiguous is left untouched, so the caller's tracked values survive.
    Task ResolveAsync<T>(IList<T> rows, string storeId, Func<T, string> getCode, Action<T, SalesRepActivityProduct> setProduct);
}
