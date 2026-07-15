using System.Threading.Tasks;
using GraphQL;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomersQueryBuilder : SalesRepSearchQueryBuilder<SalesRepCustomersQuery, SalesRepCustomerSearchResult, SalesRepCustomer, SalesRepCustomerType>
{
    protected override string Name => "salesRepCustomers";

    public SalesRepCustomersQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SalesRepCustomersQuery request)
    {
        // Runs the shared authenticated-Sales-Rep guard first.
        await base.BeforeMediatorSend(context, request);

        // Propagate this field's arguments (notably cultureName) to the UserContext so the nested SalesRepOrderType
        // resolvers on each customer's lastOrder (statusDisplayValue, total.formattedAmount) can read the culture
        // (the X-Order pattern; mirrors SalesRepOrdersQueryBuilder).
        context.CopyArgumentsToUserContext();
    }
}
