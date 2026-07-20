using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

/// <summary>
/// Builds the <c>sendCustomerCommunication</c> mutation on the Sales Rep scoped schema. Enforces the module-wide
/// rule (the caller must be an authenticated Sales Rep) and sets the caller's identity server-side; the handler
/// then verifies the Rep actually serves the target organization before sending.
/// </summary>
public class SendCustomerCommunicationCommandBuilder
    : CommandBuilder<SendCustomerCommunicationCommand, bool, InputSendCustomerCommunicationType, BooleanGraphType>
{
    protected override string Name => "sendCustomerCommunication";

    public SendCustomerCommunicationCommandBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SendCustomerCommunicationCommand request)
    {
        await base.BeforeMediatorSend(context, request);

        context.EnsureAuthenticated();

        request.UserId = context.GetCurrentUserId();
    }
}
