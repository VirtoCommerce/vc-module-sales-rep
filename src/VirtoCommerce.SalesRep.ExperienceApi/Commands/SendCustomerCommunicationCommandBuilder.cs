using System;
using System.Threading.Tasks;
using GraphQL;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class SendCustomerCommunicationCommandBuilder
    : CommandBuilder<SendCustomerCommunicationCommand, SalesRepCommunicationResult, InputSendCustomerCommunicationType, SalesRepCommunicationResultType>
{
    protected override string Name => "sendCustomerCommunication";

    public SendCustomerCommunicationCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    [Obsolete("Use the constructor without IMediator. The mediator is resolved from context.RequestServices per request.", DiagnosticId = "VC0015", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
    public SendCustomerCommunicationCommandBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : this(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SendCustomerCommunicationCommand request)
    {
        await base.BeforeMediatorSend(context, request);

        context.EnsureAuthenticated();

        request.UserId = context.GetCurrentUserId();
    }
}
