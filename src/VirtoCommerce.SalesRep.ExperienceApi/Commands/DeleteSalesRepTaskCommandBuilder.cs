using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class DeleteSalesRepTaskCommandBuilder : SalesRepCommandBuilder<DeleteSalesRepTaskCommand, bool, InputDeleteSalesRepTaskType, BooleanGraphType>
{
    protected override string Name => "deleteSalesRepTask";

    public DeleteSalesRepTaskCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, DeleteSalesRepTaskCommand request)
    {
        await base.BeforeMediatorSend(context, request);

        request.UserId = context.GetCurrentUserId();
        request.MemberId = context.GetCurrentMemberId();
    }
}
