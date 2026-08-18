using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Platform.Security.Authorization;
using VirtoCommerce.SalesRep.Core;

namespace VirtoCommerce.SalesRep.ExperienceApi.Authorization;

// Fail-closed gate for the shared documents library — one policy encoding: the same SalesRepDocumentPermissions
// predicates the REST controller reads call. Unlike the default file-exp-api handler, there is NO ownerless-file shortcut.
public class SalesRepDocumentAuthorizationHandler : PermissionAuthorizationHandlerBase<SalesRepDocumentAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SalesRepDocumentAuthorizationRequirement requirement)
    {
        var authorized = requirement.Permission == ModuleConstants.Security.Permissions.DocumentsRead
            ? context.User.HasReadAccess()
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
}
