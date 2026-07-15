using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Translates the GraphQL field selection of a Sales Rep order query into the minimal
/// <c>CustomerOrderResponseGroup</c> needed to populate exactly those fields — so the order search loads only
/// what the caller asked for (e.g. line items only when <c>itemsCount</c> is requested, prices only for
/// <c>total</c>). Mirrors the X-Cart <c>ICartResponseGroupParser</c> pattern; shared by the <c>salesRepOrders</c>
/// list and the <c>lastOrder</c> field so the two can't drift.
/// </summary>
public interface ISalesRepOrderResponseGroupParser
{
    /// <param name="includeFields">Requested GraphQL selection paths (e.g. "items.total", "itemsCount").</param>
    /// <returns>A <c>CustomerOrderResponseGroup</c> flags string for <c>CustomerOrderSearchCriteria.ResponseGroup</c>.</returns>
    string GetResponseGroup(IList<string> includeFields);
}
