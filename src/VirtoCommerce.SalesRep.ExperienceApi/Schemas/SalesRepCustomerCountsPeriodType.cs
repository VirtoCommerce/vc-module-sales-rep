using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepCustomerCountsPeriodType : ExtendableGraphType<SalesRepCustomerCountsPeriod>
{
    public SalesRepCustomerCountsPeriodType()
    {
        Name = "SalesRepCustomerCountsPeriod";

        Field(x => x.OrderingCustomers, nullable: false).Description("Distinct customers the rep ordered for within the range.");
        Field(x => x.NewCustomers, nullable: false).Description("Customers whose first-ever order by the rep falls in the range.");
    }
}
