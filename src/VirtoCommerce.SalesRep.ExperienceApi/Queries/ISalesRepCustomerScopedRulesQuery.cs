namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// A rules discovery query whose list can be scoped to a single customer, so the vocabulary must narrow with it (the
/// storefront passes the same <c>organizationId</c> the list uses). Domains without a per-customer surface don't
/// implement it.
/// </summary>
public interface ISalesRepCustomerScopedRulesQuery
{
    string OrganizationId { get; }
}
