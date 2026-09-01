using System.Security.Claims;
using GraphQL;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Extensions;

public static class ResolveFieldContextMemberExtensions
{
    /// <summary>
    /// The caller's contact (Member) id. Task ownership is keyed on this, not on the user id: WorkTask.ResponsibleId
    /// holds a Member id everywhere in vc-module-task-management. The platform writes the claim as
    /// <c>user.MemberId ?? string.Empty</c>, so an account with no contact yields an EMPTY string, not a missing
    /// claim - callers must treat empty exactly like missing and never fall through to an unscoped query.
    /// </summary>
    public static string GetCurrentMemberId(this IResolveFieldContext context)
    {
        var memberId = context.GetCurrentPrincipal()?.FindFirstValue(PlatformConstants.Security.Claims.MemberIdClaimType);

        return string.IsNullOrEmpty(memberId) ? null : memberId;
    }
}
