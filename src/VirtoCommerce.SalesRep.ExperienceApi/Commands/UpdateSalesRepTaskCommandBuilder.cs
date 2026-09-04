using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class UpdateSalesRepTaskCommandBuilder : SalesRepCommandBuilder<UpdateSalesRepTaskCommand, SalesRepTask, InputUpdateSalesRepTaskType, SalesRepTaskType>
{
    protected override string Name => "updateSalesRepTask";

    public UpdateSalesRepTaskCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
