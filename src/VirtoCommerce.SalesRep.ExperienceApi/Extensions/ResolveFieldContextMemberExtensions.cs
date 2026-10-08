using System.Security.Claims;
using GraphQL;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Extensions;

public static class ResolveFieldContextMemberExtensions
{
    // The platform writes the claim as `user.MemberId ?? string.Empty`, so an account with no contact yields an
    // EMPTY string rather than a missing claim. Normalized to null here so callers cannot read it as "no filter".
    public static string GetCurrentMemberId(this IResolveFieldContext context)
    {
        var memberId = context.GetCurrentPrincipal()?.FindFirstValue(PlatformConstants.Security.Claims.MemberIdClaimType);

        return string.IsNullOrEmpty(memberId) ? null : memberId;
    }
}
