using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Source of the "My customers" list orderings (sort options) and the mapping that resolves a selection to a
/// <see cref="SalesRepCustomerSortSpec"/> — a spec, not a bare sort string, because some orderings (last-order date,
/// period purchases) are derived from the rep's orders and the members search can't sort by them. The default
/// exposes "my last orders" (default), "ytd purchases" and "name"; a project replaces this service (DI
/// last-registration wins) to add orderings. An unknown/empty selection resolves to the default ordering (a sort
/// never fails closed).
/// </summary>
public interface ISalesRepCustomerSortRuleResolver : ISortRuleResolver<SalesRepCustomerSortRule>
{
    /// <summary>Resolves the selected (or default) ordering to a concrete spec the customers handler applies.</summary>
    Task<SalesRepCustomerSortSpec> ResolveSortAsync(string storeId, string sort);
}
