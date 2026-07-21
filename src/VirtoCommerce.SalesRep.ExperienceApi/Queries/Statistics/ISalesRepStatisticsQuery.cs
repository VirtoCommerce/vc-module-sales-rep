namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

/// <summary>
/// The request shape shared by the standalone sales-rep statistics queries (orders, carts): the fields the common
/// handler base reads to build the security-scoped context. Each concrete query keeps its own GraphQL arguments
/// (the descriptions differ per domain), so this interface deliberately covers only the shared inputs.
/// </summary>
public interface ISalesRepStatisticsQuery
{
    /// <summary>Organization (customer) id to scope to; null/empty = all the rep's assigned customers.</summary>
    string OrganizationId { get; }

    /// <summary>Store to scope to; null = all stores.</summary>
    string StoreId { get; }

    /// <summary>Requested target currency; null = the store's default, then the platform primary.</summary>
    string CurrencyCode { get; }

    /// <summary>The calling sales rep's security-account id (set server-side from the caller's claims).</summary>
    string UserId { get; }
}
