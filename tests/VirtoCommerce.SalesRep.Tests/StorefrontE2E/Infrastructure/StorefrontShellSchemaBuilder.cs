using System;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.ProfileExperienceApiModule.Data.Models;
using VirtoCommerce.ProfileExperienceApiModule.Data.Queries;
using VirtoCommerce.ProfileExperienceApiModule.Data.Queries.AddressesQuery;
using VirtoCommerce.ProfileExperienceApiModule.Data.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Queries;
using VirtoCommerce.XCart.Core.Queries;
using VirtoCommerce.XCart.Core.Schemas;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Schemas;
using XapiModuleConstants = VirtoCommerce.Xapi.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The root fields the storefront shell asks for before and around any sales-rep page, captured from the real
/// storefront on 2026-10-01, and on 2026-11-05 for the address book that replaced the organization's addresses:
/// pageContext, childCategories, cart, menu, searchHistory, organization, currentOrganizationAddresses (plus store,
/// registered through the real Xapi builder). Schema-complete, so every selection set the storefront sends
/// validates against the REAL response types; data-empty wherever the data is not under test. A root field that is
/// simply missing fails the whole operation with a validation error, and the storefront toasts those.
/// </summary>
internal sealed class StorefrontShellSchemaBuilder : ISchemaBuilder
{
    public void Build(ISchema schema)
    {
        // What X-Frontend's PageContextQueryBuilder composes: user + store + slugInfo (white-labeling is gated off
        // client-side because the module is not in the store's module list).
        schema.Query.AddField(new FieldType
        {
            Name = "pageContext",
            Type = typeof(PageContextResponseType),
            Arguments = new QueryArguments(
                new QueryArgument<StringGraphType> { Name = "userId" },
                new QueryArgument<StringGraphType> { Name = "organizationId" },
                new QueryArgument<StringGraphType> { Name = "domain" },
                new QueryArgument<StringGraphType> { Name = "storeId" },
                new QueryArgument<StringGraphType> { Name = "permalink" },
                new QueryArgument<StringGraphType> { Name = "cultureName" }),
            Resolver = new FuncFieldResolver<object>(ResolvePageContextAsync),
        });

        // The arguments come from the real query classes, so they match what the storefront's codegen saw.
        AddEmpty(schema, "childCategories", typeof(ChildCategoriesQueryResponseType), Arguments<ChildCategoriesQuery>(),
            _ => new ChildCategoriesQueryResponse { ChildCategories = [] });
        AddEmpty(schema, "cart", typeof(CartType), Arguments<GetCartQuery>(), _ => null);
        AddEmpty(schema, "organization", typeof(OrganizationType),
            new QueryArguments(
                new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "id" },
                new QueryArgument<StringGraphType> { Name = "userId" }),
            _ => null);

        // X-Profile's address book of the organization a rep signed in for, which the storefront reads on every load
        // for its ship-to selector since vc-frontend VCST-4857. The real field is a connection: `first` and `after` come
        // with it, not with the query's own arguments.
        var addressArguments = Arguments<CurrentOrganizationAddressesQuery>();
        addressArguments.Add(new QueryArgument<IntGraphType> { Name = "first" });
        addressArguments.Add(new QueryArgument<StringGraphType> { Name = "after" });
        AddEmpty(schema, "currentOrganizationAddresses", typeof(MemberAddressConnectionType<MemberAddressType>), addressArguments,
            _ => new MemberAddressConnection<MemberAddress>([], 0, 0, 0));

        AddEmpty(schema, "menu", typeof(MenuLinkListType),
            new QueryArguments(
                new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "storeId" },
                new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "cultureName" },
                new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "name" }),
            context => new MenuLinkList { Name = context.GetArgument<string>("name"), Items = [] });

        AddEmpty(schema, "searchHistory", typeof(SearchHistoryResultType),
            new QueryArguments(
                new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "storeId" },
                new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "maxCount" }),
            _ => new SearchHistoryResult { Queries = [] });
    }

    private static async ValueTask<object> ResolvePageContextAsync(IResolveFieldContext context)
    {
        var mediator = context.RequestServices!.GetRequiredService<IMediator>();

        var store = await mediator.Send(new GetStoreQuery
        {
            Domain = context.GetArgument<string>("domain"),
            StoreId = context.GetArgument<string>("storeId"),
            CultureName = context.GetArgument<string>("cultureName"),
        });

        var userName = context.GetCurrentPrincipal()?.Identity?.Name;
        var user = string.IsNullOrEmpty(userName)
            ? new ApplicationUser { Id = context.GetArgument<string>("userId") ?? string.Empty, UserName = XapiModuleConstants.AnonymousUser.UserName }
            : await mediator.Send(new GetUserQuery { UserName = userName });

        return new PageContextResponse { Store = store, User = user, SlugInfo = new SlugInfoResponse() };
    }

    private static QueryArguments Arguments<TQuery>() where TQuery : IHasArguments, new()
        => new(new TQuery().GetArguments());

    private static void AddEmpty(ISchema schema, string name, Type graphType, QueryArguments arguments, Func<IResolveFieldContext, object> resolve)
    {
        schema.Query.AddField(new FieldType
        {
            Name = name,
            Type = graphType,
            Arguments = arguments,
            Resolver = new FuncFieldResolver<object>(context => new ValueTask<object>(resolve(context))),
        });
    }
}
