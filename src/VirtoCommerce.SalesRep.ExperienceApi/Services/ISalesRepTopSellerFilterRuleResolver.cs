using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Source of the "Top Sellers" category badges (filter options) and the mapping that applies a selection to the
/// ranking criteria. The default exposes the store catalog's top-level non-hidden categories (1:1) and, on selection,
/// restricts the ranking to that category's subtree. A project replaces this service (DI last-registration wins) to
/// group categories or add custom rules.
/// </summary>
public interface ISalesRepTopSellerFilterRuleResolver : IFilterRuleResolver<SalesRepTopSellerFilterRule>
{
    /// <summary>
    /// Applies the selected category badge to the ranking criteria (restricts to the category's subtree) and returns
    /// it. Returns the criteria unchanged when <paramref name="filter"/> is null/empty (all categories), and
    /// <c>null</c> when a category name was given but is unrecognized (fail-closed).
    /// </summary>
    Task<SalesRepTopSellerCriteria> ApplyFilterAsync(string storeId, string filter, SalesRepTopSellerCriteria criteria);
}
