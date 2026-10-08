using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class CreateSalesRepTaskCommandBuilder : SalesRepCommandBuilder<CreateSalesRepTaskCommand, SalesRepTask, InputCreateSalesRepTaskType, SalesRepTaskType>
{
    protected override string Name => "createSalesRepTask";

    public CreateSalesRepTaskCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
