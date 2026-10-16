using System;
using GraphQL;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.ProfileExperienceApiModule.Data.Aggregates;
using VirtoCommerce.ProfileExperienceApiModule.Data.Schemas;
using VirtoCommerce.ProfileExperienceApiModule.Data.Services;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Services;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The storefront is built against a newer profile API than the package this project pins (3.1011): its
/// organization fragment selects <c>isLockedForCurrentUser</c>, a field that arrived together with membership
/// locking in later customer and profile packages. Rather than bumping those packages (which drags Customer.Core
/// past the harness's Customer.Data pin), the field is added here and answers "not locked", since the pinned
/// customer package has no membership lock to read. This subclass is registered last for
/// <see cref="OrganizationType"/>, so GraphQL.NET resolves it everywhere the schema references the organization.
/// </summary>
internal sealed class StorefrontOrganizationType : OrganizationType
{
    public StorefrontOrganizationType(
        IStoreService storeService,
        IDynamicPropertyResolverService dynamicPropertyResolverService,
        IMemberAddressService memberAddressService,
        IMediator mediator,
        IMemberAggregateFactory factory,
        IMemberService memberService,
        IMemberSearchService memberSearchService,
        IOrganizationMembershipSearchService organizationMembershipService,
        Func<RoleManager<Role>> roleManagerFactory,
        Func<UserManager<ApplicationUser>> userManagerFactory)
        : base(storeService, dynamicPropertyResolverService, memberAddressService, mediator, factory, memberService,
            memberSearchService, organizationMembershipService, roleManagerFactory, userManagerFactory)
    {
        Field<BooleanGraphType>("isLockedForCurrentUser")
            .Description("Whether the current user's membership in this organization is locked (always false: the pinned customer package has no membership lock)")
            .Resolve(_ => false);
    }
}
