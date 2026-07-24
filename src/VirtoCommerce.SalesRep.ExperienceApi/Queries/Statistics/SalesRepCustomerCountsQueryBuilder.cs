using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

public class SalesRepCustomerCountsQueryBuilder : SalesRepQueryBuilder<SalesRepCustomerCountsQuery, SalesRepCustomerCountsContext, SalesRepCustomerCountsType>
{
    protected override string Name => "salesRepCustomerCounts";

    public SalesRepCustomerCountsQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
