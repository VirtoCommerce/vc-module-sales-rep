using System;
using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

internal static class OrganizationNameFieldExtensions
{
    // Shared by every type that exposes organizationName, so one query resolves all of them in a single member
    // batch instead of one batch per type.
    private const string LoaderKey = "SalesRep.OrganizationNameById";

    public static void AddOrganizationNameField<TSource>(
        this ObjectGraphType<TSource> graphType,
        IDataLoaderContextAccessor dataLoaderContextAccessor,
        IMemberService memberService,
        Func<TSource, string> getOrganizationName,
        Func<TSource, string> getOrganizationId)
    {
        graphType.Field<StringGraphType>("organizationName")
            .Description("Organization (customer) name.")
            .Resolve(context =>
            {
                var organizationName = getOrganizationName(context.Source);
                if (!string.IsNullOrEmpty(organizationName))
                {
                    return organizationName;
                }

                var organizationId = getOrganizationId(context.Source);
                if (string.IsNullOrEmpty(organizationId))
                {
                    return null;
                }

                var loader = dataLoaderContextAccessor.Context.GetOrAddBatchLoader<string, string>(
                    LoaderKey,
                    async organizationIds =>
                    {
                        var organizations = await memberService.GetByIdsAsync(
                            organizationIds.ToArray(),
                            nameof(MemberResponseGroup.Default),
                            [nameof(Organization)]);

                        // DistinctBy first: the loader asks for distinct ids, but ToDictionary throws on a
                        // duplicate key rather than answering, and a member service is free to return one.
                        return organizations
                            .DistinctBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(x => x.Id, x => x.Name, StringComparer.OrdinalIgnoreCase);
                    });

                return loader.LoadAsync(organizationId);
            });
    }
}
