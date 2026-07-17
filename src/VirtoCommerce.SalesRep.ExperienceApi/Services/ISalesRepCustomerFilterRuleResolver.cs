using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Source of the Sales Rep customer segments (filter options) shared by both customer readers — the customers list
/// (<see cref="ApplyListFilterAsync"/>) and the "my customers" counts (<see cref="ApplyCountsFilterAsync"/>) — so a
/// segment name means the same thing in both. The default implementation defines no segments (a customer segment is
/// a behavioral predicate the module can't express generically); a project replaces this service (DI last-wins) to
/// add segments, and — for a predicate the standard criteria can't carry — subclasses the reader (narrowing the
/// members search criteria's <c>ObjectIds</c> for the list, or the counts service's <c>BuildQuery</c> seam).
/// </summary>
public interface ISalesRepCustomerFilterRuleResolver : IFilterRuleResolver<SalesRepCustomerFilterRule>
{
    /// <summary>
    /// Applies the selected segment to the customers-list members search criteria and returns it. Returns the
    /// criteria unchanged when <paramref name="filter"/> is null/empty (all served customers), and <c>null</c> when
    /// a segment name was given but is unrecognized (fail-closed).
    /// </summary>
    Task<MembersSearchCriteria> ApplyListFilterAsync(string storeId, string filter, MembersSearchCriteria criteria);

    /// <summary>The same segment applied to the "my customers" counts criteria. <c>null</c> = fail-closed.</summary>
    Task<SalesRepCustomerCountsCriteria> ApplyCountsFilterAsync(string storeId, string filter, SalesRepCustomerCountsCriteria criteria);
}
