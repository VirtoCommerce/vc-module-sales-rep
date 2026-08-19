using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.FileExperienceApi.Core.Extensions;
using VirtoCommerce.Platform.Security.Authorization;
using VirtoCommerce.SalesRep.Core;

namespace VirtoCommerce.SalesRep.ExperienceApi.Authorization;

// Fail-closed gate for the shared documents library — one policy encoding: the same SalesRepDocumentPermissions
// predicates the REST controller reads call. Unlike the default file-exp-api handler, there is NO ownerless-file
// shortcut; on the contrary, a scope file is READABLE only once claimed as a library document (owner stamp set
// by CreateAsync) — an uploaded-but-unregistered blob is not a document and must not be served to anyone.
// Write/delete stay permission-only so documents:write holders can clean up abandoned uploads.
public class SalesRepDocumentAuthorizationHandler : PermissionAuthorizationHandlerBase<SalesRepDocumentAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SalesRepDocumentAuthorizationRequirement requirement)
    {
        var authorized = requirement.Permission == ModuleConstants.Security.Permissions.DocumentsRead
            ? context.User.HasReadAccess() && IsReadableFile(requirement)
            : context.User.HasWriteAccess();

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

    // File is null for list-level checks (the GraphQL queries, which are metadata-driven anyway).
    protected virtual bool IsReadableFile(SalesRepDocumentAuthorizationRequirement requirement)
    {
        return requirement.File == null || !requirement.File.OwnerIsEmpty();
    }
}
