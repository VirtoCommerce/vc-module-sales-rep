using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.FileExperienceApi.Core.Authorization;
using VirtoCommerce.FileExperienceApi.Core.Models;
using VirtoCommerce.SalesRep.Core;
using FilePermissions = VirtoCommerce.FileExperienceApi.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Authorization;

// Reroutes the generic file surfaces (GET /api/files/{id}, deleteFile) for the library scope to the module's
// handler — the default file-exp-api handler would grant any ownerless file, even to anonymous callers.
public class SalesRepDocumentAuthorizationRequirementFactory : IFileAuthorizationRequirementFactory
{
    public string Scope => ModuleConstants.DocumentsScope;

    public IAuthorizationRequirement Create(File file, string permission)
    {
        // Read maps to documents:read; every other operation (create/update/delete/unknown) fails closed to documents:write.
        var documentPermission = permission == FilePermissions.Read
            ? ModuleConstants.Security.Permissions.DocumentsRead
            : ModuleConstants.Security.Permissions.DocumentsWrite;

        return new SalesRepDocumentAuthorizationRequirement(documentPermission);
    }
}
