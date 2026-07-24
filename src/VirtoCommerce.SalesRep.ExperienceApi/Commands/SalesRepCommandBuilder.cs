using System;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

// Mirrors SalesRepQueryBuilder for mutations: centralizes the authentication gate so every Sales Rep command
// builder just maps its request.
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

    [Obsolete("Use the constructor without IMediator. The mediator is resolved from context.RequestServices per request.", DiagnosticId = "VC0015", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
    protected SalesRepCommandBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : this(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, TCommand request)
    {
        await base.BeforeMediatorSend(context, request);

        context.EnsureAuthenticated();
    }
}
