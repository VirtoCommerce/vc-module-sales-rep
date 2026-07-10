using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class CustomerOrderStatisticsComparisonType : ExtendableGraphType<CustomerOrderStatisticsComparison>
{
    public CustomerOrderStatisticsComparisonType()
    {
        Name = "CustomerOrderStatisticsComparison";

        Field(x => x.TotalChange, nullable: false).Description("Current total minus previous total, in the requested currency.");
        Field(x => x.TotalChangePercent, nullable: true).Description("Percentage change of total; null when the previous total is zero.");
        Field(x => x.CountChange, nullable: false).Description("Current count minus previous count.");
        Field(x => x.CountChangePercent, nullable: true).Description("Percentage change of count; null when the previous count is zero.");
        Field(x => x.AverageChange, nullable: false).Description("Current average minus previous average, in the requested currency.");
        Field(x => x.AverageChangePercent, nullable: true).Description("Percentage change of average; null when the previous average is zero.");
    }
}
