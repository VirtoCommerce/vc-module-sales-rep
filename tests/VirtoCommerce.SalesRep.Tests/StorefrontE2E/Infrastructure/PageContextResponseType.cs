using GraphQL.Types;
using VirtoCommerce.Xapi.Core.Schemas;
using ProfileUserType = VirtoCommerce.ProfileExperienceApiModule.Data.Schemas.UserType;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

internal sealed class PageContextResponseType : ObjectGraphType<PageContextResponse>
{
    public PageContextResponseType()
    {
        Name = "PageContextResponseType";
        Field<StoreResponseType>("store").Resolve(context => context.Source.Store);
        Field<SlugInfoResponseType>("slugInfo").Resolve(context => context.Source.SlugInfo);
        Field<ProfileUserType>("user").Resolve(context => context.Source.User);
    }
}
