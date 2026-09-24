using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class DeleteSalesRepTaskCommandBuilder : SalesRepCommandBuilder<DeleteSalesRepTaskCommand, bool, InputDeleteSalesRepTaskType, BooleanGraphType>
{
    protected override string Name => "deleteSalesRepTask";

    public DeleteSalesRepTaskCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
