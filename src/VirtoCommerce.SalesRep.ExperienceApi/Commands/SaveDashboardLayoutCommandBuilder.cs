using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas.Dashboard;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class SaveDashboardLayoutCommandBuilder
    : SalesRepCommandBuilder<SaveDashboardLayoutCommand, DashboardLayout, InputSalesRepDashboardLayoutType, SalesRepDashboardLayoutType>
{
    protected override string Name => "saveSalesRepDashboardLayout";

    public SaveDashboardLayoutCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SaveDashboardLayoutCommand request)
    {
        await base.BeforeMediatorSend(context, request);

        request.UserId = context.GetCurrentUserId();
    }
}
