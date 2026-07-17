using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Source of the Sales Rep orders-list orderings (sort options) and the mapping that applies a selection to the
/// order search criteria. The default exposes a single "recent" rule (newest first); a project replaces this service
/// (DI last-registration wins) to add orderings (e.g. "biggest by total"). A sort only reorders, so an unknown/empty
/// selection resolves to the default ordering rather than failing closed.
/// </summary>
public interface ISalesRepOrderSortRuleResolver : ISortRuleResolver<SalesRepOrderSortRule>
{
    /// <summary>Applies the selected (or default) ordering to the orders-list search criteria and returns it.</summary>
    Task<CustomerOrderSearchCriteria> ApplySortAsync(string storeId, string sort, CustomerOrderSearchCriteria criteria);
}
