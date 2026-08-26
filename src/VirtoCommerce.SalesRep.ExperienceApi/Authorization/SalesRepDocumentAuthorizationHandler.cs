using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.FileExperienceApi.Core.Extensions;
using VirtoCommerce.FileExperienceApi.Core.Models;
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
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, SalesRepDocumentAuthorizationRequirement requirement)
    {
        await base.HandleRequirementAsync(context, requirement);

        if (context.PendingRequirements.Contains(requirement))
        {
            // No permission — fail decisively: the platform default handler also processes this requirement
            // type, and its Succeed cannot outvote a Fail.
            context.Fail();
            return;
        }

        // Null for list-level checks (the GraphQL queries) — the permission alone decides. The generic file
        // surfaces pass the file as the resource (FileAuthorizationService); the gate below can only veto.
        if (context.Resource is not File file)
        {
            return;
        }

        var authorized = requirement.Permission == ModuleConstants.Security.Permissions.DocumentsRead
            ? IsReadableFile(file)
            : IsWritableFile(file);

        if (!authorized)
        {
            context.Fail();
        }
    }

    // The read gate requires the complete claim CreateAsync writes — the owner id AND the library owner type
    // (a half-claim is not a document).
    protected virtual bool IsReadableFile(File file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return !string.IsNullOrEmpty(file.OwnerEntityId) && file.OwnerTypeIs<SalesRepDocumentMetadata>();
    }

    protected virtual bool IsWritableFile(File file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return file.OwnerIsEmpty();
    }
}
