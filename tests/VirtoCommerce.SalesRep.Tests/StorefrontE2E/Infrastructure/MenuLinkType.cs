using GraphQL.Types;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

internal sealed class MenuLinkType : ObjectGraphType<MenuLink>
{
    public MenuLinkType()
    {
        Name = "MenuLinkType";
        Field<StringGraphType>("title").Resolve(context => context.Source.Title);
        Field<StringGraphType>("url").Resolve(context => context.Source.Url);
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<MenuLinkType>>>>("childItems").Resolve(context => context.Source.ChildItems);
    }
}
