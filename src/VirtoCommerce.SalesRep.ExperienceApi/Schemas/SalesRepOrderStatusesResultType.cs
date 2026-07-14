using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepOrderStatusesResultType : ExtendableGraphType<SalesRepOrderStatusesResult>
{
    public SalesRepOrderStatusesResultType()
    {
        Name = "SalesRepOrderStatusesResult";

        Field<NonNullGraphType<ListGraphType<SalesRepOrderStatusType>>>("items")
            .Description("The order-status tabs for the orders panel, in display order.")
            .Resolve(context => context.Source.Items);
    }
}
