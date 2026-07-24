using System;
using System.Threading.Tasks;
using GraphQL;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas.Dashboard;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class SaveDashboardLayoutCommandBuilder
    : CommandBuilder<SaveDashboardLayoutCommand, DashboardLayout, InputSalesRepDashboardLayoutType, SalesRepDashboardLayoutType>
{
    protected override string Name => "saveSalesRepDashboardLayout";

    public SaveDashboardLayoutCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    [Obsolete("Use the constructor without IMediator. The mediator is resolved from context.RequestServices per request.", DiagnosticId = "VC0015", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
    public SaveDashboardLayoutCommandBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : this(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SaveDashboardLayoutCommand request)
    {
        await base.BeforeMediatorSend(context, request);

        context.EnsureAuthenticated();

        request.UserId = context.GetCurrentUserId();
    }
}
