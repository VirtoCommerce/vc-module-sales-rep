using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class ShareListWithCustomersCommandBuilder
    : SalesRepCommandBuilder<ShareListWithCustomersCommand, SalesRepShareListResult, InputShareListWithCustomersType, SalesRepShareListResultType>
{
    protected override string Name => "shareListWithCustomers";

    public ShareListWithCustomersCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, ShareListWithCustomersCommand request)
    {
        await base.BeforeMediatorSend(context, request);

        request.UserId = context.GetCurrentUserId();
    }
}
