using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Source of the Sales Rep cart "kinds" (filter options) and the mapping that applies a selection to the cart
/// statistics criteria (type + status). The cart analogue of <see cref="ISalesRepOrderStatusService"/>. Only a
/// statistics apply method exists today (there is no cart list yet); a cart list would add its own apply method
/// here, keeping both mappings in one class. The default implementation exposes a single built-in "project" kind
/// (cart type "Wishlist"); a project replaces this service to hide, add or recompose kinds.
/// </summary>
public interface ISalesRepCartKindService : IFilterRuleResolver<SalesRepCartKind>
{
    /// <summary>
    /// Applies the selected kinds' type/status filter to the cart-statistics criteria and returns it. Returns the
    /// criteria unchanged when no kinds were selected, and <c>null</c> when kinds were selected but none resolved
    /// (fail-closed).
    /// </summary>
    Task<CustomerCartStatisticsCriteria> ApplyStatisticsFilterAsync(string storeId, IList<string> selectedNames, CustomerCartStatisticsCriteria criteria);
}
