using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// A rules discovery query whose vocabulary is derived from records in a date window, so the caller's selected period
/// has to reach the resolver (order statuses, top-seller categories). Domains with a static vocabulary don't implement
/// it.
/// </summary>
public interface ISalesRepPeriodScopedRulesQuery
{
    SalesRepStatisticsPeriodInput Period { get; }
}
