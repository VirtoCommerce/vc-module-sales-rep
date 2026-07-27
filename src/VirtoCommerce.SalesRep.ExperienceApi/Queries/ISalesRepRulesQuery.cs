namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Shape shared by the rule-discovery queries (order/cart/customer/top-seller filter and sort rules). Carries the
/// caller's server-resolved <see cref="UserId"/> so the handler can gate the vocabulary on a sales-rep membership,
/// plus the store/culture the rules are read for.
/// </summary>
public interface ISalesRepRulesQuery
{
    string UserId { get; }

    string StoreId { get; }

    string CultureName { get; }
}
