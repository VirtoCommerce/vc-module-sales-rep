using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public abstract class SalesRepCommandBuilder<TCommand, TResult, TCommandGraphType, TResultGraphType>
    : CommandBuilder<TCommand, TResult, TCommandGraphType, TResultGraphType>
    where TCommand : IRequest<TResult>
    where TCommandGraphType : IInputObjectGraphType
    where TResultGraphType : IGraphType
{
    protected SalesRepCommandBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, TCommand request)
    {
        await base.BeforeMediatorSend(context, request);

        await context.EnsureAuthenticatedAsync();

        // Stamped here, not per builder, so identity always comes from the token and never from input.
        if (request is ISalesRepUserCommand userCommand)
        {
            userCommand.UserId = context.GetCurrentUserId();
        }

        if (request is ISalesRepMemberCommand memberCommand)
        {
            memberCommand.MemberId = context.GetCurrentMemberId();
        }
    }
}
