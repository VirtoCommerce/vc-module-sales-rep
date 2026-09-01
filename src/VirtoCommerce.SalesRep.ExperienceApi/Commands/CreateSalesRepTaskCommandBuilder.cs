using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class CreateSalesRepTaskCommandBuilder : SalesRepCommandBuilder<CreateSalesRepTaskCommand, SalesRepTask, InputCreateSalesRepTaskType, SalesRepTaskType>
{
    protected override string Name => "createSalesRepTask";

    public CreateSalesRepTaskCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, CreateSalesRepTaskCommand request)
    {
        await base.BeforeMediatorSend(context, request);

        request.UserId = context.GetCurrentUserId();
        request.MemberId = context.GetCurrentMemberId();
    }
}
