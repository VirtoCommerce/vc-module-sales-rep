using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.XOrder.Core;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerOrderQuery : Query<CustomerOrderAggregate>
{
    public string Id { get; set; }

    public string CultureName { get; set; }

    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(Id), "Order id. The order must belong to a customer the rep serves.");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for the localized statusDisplayValue field (\"en-US\").");
    }

    public override void Map(IResolveFieldContext context)
    {
        Id = context.GetArgument<string>(nameof(Id));
        CultureName = context.GetArgument<string>(nameof(CultureName));
        UserId = context.GetCurrentUserId();
    }
}
