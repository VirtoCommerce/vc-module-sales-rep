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
