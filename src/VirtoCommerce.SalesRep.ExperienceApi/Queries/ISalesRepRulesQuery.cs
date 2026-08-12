namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public interface ISalesRepRulesQuery
{
    string StoreId { get; }

    string CultureName { get; }

    string UserId { get; }
}
