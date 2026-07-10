using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class CustomerOrderStatisticsPeriodType : ExtendableGraphType<CustomerOrderStatisticsPeriod>
{
    public CustomerOrderStatisticsPeriodType()
    {
        Name = "CustomerOrderStatisticsPeriod";

        Field(x => x.Total, nullable: false).Description("Sum of order totals in the range, in the requested currency.");
        Field(x => x.Count, nullable: false).Description("Number of orders in the range.");
        Field(x => x.Average, nullable: false).Description("Average order value in the range, in the requested currency.");
        Field(x => x.LastOrderDate, nullable: true).Description("Date of the most recent order in the range.");
    }
}
