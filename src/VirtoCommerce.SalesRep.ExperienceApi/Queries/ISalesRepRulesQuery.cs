namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Shape shared by the rule-discovery queries (order/cart/customer/top-seller filter and sort rules). Carries the
/// caller's server-resolved <see cref="UserId"/> so the handler can gate the vocabulary on a sales-rep membership.
/// Store/culture stay on the concrete queries — the gate reads only the user.
/// </summary>
public interface ISalesRepRulesQuery
{
    string UserId { get; }
}
