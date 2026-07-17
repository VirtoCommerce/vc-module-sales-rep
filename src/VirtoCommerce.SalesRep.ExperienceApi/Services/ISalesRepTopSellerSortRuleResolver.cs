using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Source of the "Top Sellers" orderings (sort options) and the mapping that applies a selection to the ranking
/// criteria. The default exposes "by-units" (default) and "by-revenue"; a project replaces this service (DI
/// last-registration wins) to add orderings. A sort only reorders, so an unknown/empty selection resolves to the
/// default ordering rather than failing closed.
/// </summary>
public interface ISalesRepTopSellerSortRuleResolver : ISortRuleResolver<SalesRepTopSellerSortRule>
{
    /// <summary>Applies the selected (or default) ordering to the ranking criteria and returns it.</summary>
    Task<SalesRepTopSellerCriteria> ApplySortAsync(string storeId, string sort, SalesRepTopSellerCriteria criteria);
}
