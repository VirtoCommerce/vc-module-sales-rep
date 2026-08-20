using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.FileExperienceApi.Core.Extensions;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security.Authorization;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Authorization;

// Fail-closed gate for the shared documents library. Unlike the default file-exp-api handler, there is NO
// ownerless-file shortcut; on the contrary, a scope file is READABLE only once claimed as a library document
// (owner stamp set by CreateAsync) — an uploaded-but-unregistered blob is not a document and must not be served
// to anyone. The mirror rule guards writes: the generic file surfaces (deleteFile) may touch only UNCLAIMED
// files (abandoned-upload cleanup by documents:write holders); claimed documents are managed exclusively
// through the module's own endpoints, which keep the metadata row and the file in step.
public class SalesRepDocumentAuthorizationHandler : PermissionAuthorizationHandlerBase<SalesRepDocumentAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SalesRepDocumentAuthorizationRequirement requirement)
    {
        // Any non-read permission fails closed to the write branch.
        var authorized = requirement.Permission == ModuleConstants.Security.Permissions.DocumentsRead
            ? HasPermission(context.User, ModuleConstants.Security.Permissions.DocumentsRead) && IsReadableFile(requirement)
            : HasPermission(context.User, ModuleConstants.Security.Permissions.DocumentsWrite) && IsWritableFile(requirement);

        if (authorized)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }

        return Task.CompletedTask;
    }

    // Mirrors the platform permission handler: a limited token grants only its listed permissions
    // (even for Administrators); otherwise Administrator passes everything.
    protected virtual bool HasPermission(ClaimsPrincipal user, string permission)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var limitedPermissionsClaim = user.FindFirstValue(PlatformConstants.Security.Claims.LimitedPermissionsClaimType);
        if (limitedPermissionsClaim != null)
        {
            return limitedPermissionsClaim
                .Split(PlatformConstants.Security.Claims.PermissionClaimTypeDelimiter, StringSplitOptions.RemoveEmptyEntries)
                .Contains(permission);
        }

        return user.HasGlobalPermission(permission);
    }

    // File is null for list-level checks (the GraphQL queries, which are metadata-driven anyway).
    // The read gate requires the complete claim CreateAsync writes: the owner id AND the library owner type.
    protected virtual bool IsReadableFile(SalesRepDocumentAuthorizationRequirement requirement)
    {
        var file = requirement.File;

        return file == null || (!string.IsNullOrEmpty(file.OwnerEntityId) && file.OwnerTypeIs<SalesRepDocumentMetadata>());
    }

    protected virtual bool IsWritableFile(SalesRepDocumentAuthorizationRequirement requirement)
    {
        return requirement.File == null || requirement.File.OwnerIsEmpty();
    }
}
