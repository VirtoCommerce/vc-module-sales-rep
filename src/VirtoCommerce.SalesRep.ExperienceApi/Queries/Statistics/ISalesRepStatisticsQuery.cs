namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

public interface ISalesRepStatisticsQuery
{
    string OrganizationId { get; }

    string StoreId { get; }

    string CurrencyCode { get; }

    string UserId { get; }
}
