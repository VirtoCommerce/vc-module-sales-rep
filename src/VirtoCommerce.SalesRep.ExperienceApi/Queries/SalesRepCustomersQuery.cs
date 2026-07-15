using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Query for the customer organizations the current Sales Rep is responsible for (VCST-5304).
/// The Sales Rep is the caller; their security account id is set server-side from the caller's claims.
/// </summary>
public class SalesRepCustomersQuery : SearchQuery<SalesRepCustomerSearchResult>
{
    /// <summary>Security account id of the current Sales Rep (set server-side from the caller's claims).</summary>
    public string UserId { get; set; }

    /// <summary>Optional store to scope each customer's <c>lastOrder</c> to (the storefront's current store).</summary>
    public string StoreId { get; set; }

    /// <summary>
    /// Culture for the localized fields on each customer's <c>lastOrder</c> — <c>statusDisplayValue</c> and
    /// <c>total.formattedAmount</c> (e.g. "en-US"). Consumed by the <c>SalesRepOrderType</c> resolvers via the
    /// request context (the builder copies it to the UserContext), not by this handler.
    /// </summary>
    public string CultureName { get; set; }

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
    }
}
