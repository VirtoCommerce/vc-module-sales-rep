using GraphQL.Types;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

internal sealed class MenuLinkListType : ObjectGraphType<MenuLinkList>
{
    public MenuLinkListType()
    {
        Name = "MenuLinkListType";
        Field<NonNullGraphType<StringGraphType>>("name").Resolve(context => context.Source.Name);
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<MenuLinkType>>>>("items").Resolve(context => context.Source.Items);
    }
}
