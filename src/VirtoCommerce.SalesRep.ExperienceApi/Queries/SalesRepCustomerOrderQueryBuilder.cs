using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.XOrder.Core;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerOrderQueryBuilder : SalesRepOrderQueryBuilder<SalesRepCustomerOrderQuery, CustomerOrderAggregate>
{
    protected override string Name => "salesRepCustomerOrder";

    public SalesRepCustomerOrderQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override string GetCultureName(SalesRepCustomerOrderQuery request) => request.CultureName;

    // An order the rep may not read comes back null, and there is then nothing to expand.
    protected override IEnumerable<CustomerOrderAggregate> GetOrderAggregates(CustomerOrderAggregate response)
        => response == null ? [] : [response];
}
