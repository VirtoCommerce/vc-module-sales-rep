using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Security.Authorization;
using VirtoCommerce.SalesRep.Core;

namespace VirtoCommerce.SalesRep.Data.Authorization;

// Fail-closed gate for the shared documents library: an authenticated caller needs documents:read for reads
// and documents:write for mutations; write implies read; platform Administrator always passes; anonymous never
// does. Unlike the default file-exp-api handler, there is NO ownerless-file shortcut.
public class SalesRepDocumentAuthorizationHandler : PermissionAuthorizationHandlerBase<SalesRepDocumentAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SalesRepDocumentAuthorizationRequirement requirement)
    {
        var authorized = context.User.Identity?.IsAuthenticated == true &&
            (context.User.IsInRole(PlatformConstants.Security.SystemRoles.Administrator) ||
             context.User.HasPermission(requirement.Permission) ||
             context.User.HasPermission(ModuleConstants.Security.Permissions.DocumentsWrite));

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
