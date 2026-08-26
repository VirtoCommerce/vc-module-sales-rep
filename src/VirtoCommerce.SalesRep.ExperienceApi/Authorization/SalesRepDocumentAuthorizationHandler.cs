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

public class SalesRepDocumentAuthorizationHandler : PermissionAuthorizationHandlerBase<SalesRepDocumentAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, SalesRepDocumentAuthorizationRequirement requirement)
    {
        await base.HandleRequirementAsync(context, requirement);

        if (context.PendingRequirements.Contains(requirement))
        {
            context.Fail();
            return;
        }

        // Null for list-level checks (the GraphQL queries) — the permission alone decides.
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
