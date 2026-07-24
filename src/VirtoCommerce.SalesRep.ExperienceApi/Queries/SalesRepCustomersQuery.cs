using System.Collections.Generic;
using System.Linq;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Index;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomersQuery : SearchQuery<SalesRepCustomerSearchResult>, IHasIncludeFields
{
    public string UserId { get; set; }

    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public IList<string> IncludeFields { get; set; } = [];

    public override IEnumerable<QueryArgument> GetArguments()
    {
        foreach (var argument in base.GetArguments())
        {
            yield return argument;
        }

        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope each customer's last order to (defaults to all stores).");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for each customer's lastOrder localized fields (statusDisplayValue, total.formattedAmount), e.g. \"en-US\".");
    }

    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);
        UserId = context.GetCurrentUserId();
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CultureName = context.GetArgument<string>(nameof(CultureName));

        IncludeFields = context.SubFields?.Values.GetAllNodesPaths(context).ToArray() ?? [];
    }
}
