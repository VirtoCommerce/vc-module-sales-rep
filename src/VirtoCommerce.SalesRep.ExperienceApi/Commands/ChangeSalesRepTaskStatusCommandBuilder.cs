using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class ChangeSalesRepTaskStatusCommandBuilder : SalesRepCommandBuilder<ChangeSalesRepTaskStatusCommand, SalesRepTask, InputChangeSalesRepTaskStatusType, SalesRepTaskType>
{
    protected override string Name => "changeSalesRepTaskStatus";

    public ChangeSalesRepTaskStatusCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
