using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class UpdateSalesRepTaskCommandBuilder : SalesRepCommandBuilder<UpdateSalesRepTaskCommand, SalesRepTask, InputUpdateSalesRepTaskType, SalesRepTaskType>
{
    protected override string Name => "updateSalesRepTask";

    public UpdateSalesRepTaskCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, UpdateSalesRepTaskCommand request)
    {
        await base.BeforeMediatorSend(context, request);

        request.UserId = context.GetCurrentUserId();
        request.MemberId = context.GetCurrentMemberId();
    }
}
