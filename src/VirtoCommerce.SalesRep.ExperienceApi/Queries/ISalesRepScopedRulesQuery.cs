using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// A rules discovery query whose vocabulary is derived from records, so it has to be built over the same records the
/// list shows: one customer when the storefront is on a customer page, and the selected period. Domains with a static
/// vocabulary (the sort rules) don't implement it.
/// </summary>
public interface ISalesRepScopedRulesQuery
{
    string OrganizationId { get; }

    SalesRepStatisticsPeriodInput Period { get; }
}
