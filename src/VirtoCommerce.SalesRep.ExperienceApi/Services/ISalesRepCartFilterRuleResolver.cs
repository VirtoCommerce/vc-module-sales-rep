using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Source of the Sales Rep cart "kinds" (filter options) and the mapping that applies a selection to the cart
/// statistics criteria (type + status). The cart analogue of <see cref="ISalesRepOrderFilterRuleResolver"/>. Only a
/// statistics apply method exists today (there is no cart list yet); a cart list would add its own apply method
/// here, keeping both mappings in one class. The default implementation exposes a single built-in "project" kind
/// (cart type "Wishlist"); a project replaces this service to hide, add or recompose kinds.
/// </summary>
public interface ISalesRepCartFilterRuleResolver : IFilterRuleResolver<SalesRepCartFilterRule>
{
    /// <summary>
    /// Applies the selected rule's type/status filter to the cart-statistics criteria and returns it. Returns the
    /// criteria unchanged when <paramref name="filter"/> is null/empty (the baseline set), and <c>null</c> when a
    /// rule name was given but is unrecognized (fail-closed).
    /// </summary>
    Task<CustomerCartStatisticsCriteria> ApplyStatisticsFilterAsync(string storeId, string filter, CustomerCartStatisticsCriteria criteria);
}
