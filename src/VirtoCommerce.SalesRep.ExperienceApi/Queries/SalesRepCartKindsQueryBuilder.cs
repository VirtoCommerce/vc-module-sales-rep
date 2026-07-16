using System.Collections.Generic;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCartKindsQueryBuilder : SalesRepQueryBuilder<SalesRepCartKindsQuery, IList<SalesRepCartKind>, ListGraphType<SalesRepCartKindType>>
{
    protected override string Name => "salesRepCartKinds";

    public SalesRepCartKindsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
