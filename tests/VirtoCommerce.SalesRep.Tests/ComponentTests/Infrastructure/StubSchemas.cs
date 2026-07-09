using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.ProfileExperienceApiModule.Data.Commands;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Minimal GraphQL schemas that let component tests drive a handler through the real <c>IDocumentExecuter</c>
/// for a field that is not on the sales-rep scoped schema.
/// </summary>
internal static class StubSchemas
{
    /// <summary>
    /// Stands in for ProfileExperienceApi's <c>organization.contacts</c>: an
    /// <c>organizationContacts(organizationId)</c> field whose resolver dispatches the real
    /// <see cref="SearchContactsQuery"/> (<c>MemberId</c> = the org id) exactly as <c>OrganizationType.contacts</c>
    /// does — so the sales-rep override handles it. (The real query lives in ProfileExperienceApi's schema, which
    /// this module's harness can't stand up.)
    /// </summary>
    public static ISchema OrganizationContacts()
    {
        var contactType = new ObjectGraphType<Member> { Name = "OrgContact" };
        contactType.Field(x => x.Id, nullable: false);
        contactType.Field(x => x.Name, nullable: true);

        var query = new ObjectGraphType { Name = "Query" };
        query.AddField(new FieldType
        {
            Name = "organizationContacts",
            ResolvedType = new ListGraphType(contactType),
            Arguments = new QueryArguments(new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "organizationId" }),
            Resolver = new FuncFieldResolver<object>(async context =>
            {
                var mediator = context.RequestServices.GetRequiredService<IMediator>();
                var result = await mediator.Send(new SearchContactsQuery
                {
                    MemberId = context.GetArgument<string>("organizationId"),
                    Skip = 0,
                    Take = 16,
                    Sort = "name:asc",
                    Keyword = string.Empty,
                });

                return result.Results;
            }),
        });

        return new Schema { Query = query };
    }
}
