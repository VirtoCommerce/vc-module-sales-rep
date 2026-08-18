using VirtoCommerce.FileExperienceApi.Core.Models;
using VirtoCommerce.Platform.Security.Authorization;

namespace VirtoCommerce.SalesRep.ExperienceApi.Authorization;

// Carries the file (may be null for list-level checks) so per-subfolder rules can be added later.
public class SalesRepDocumentAuthorizationRequirement : PermissionAuthorizationRequirement
{
    public SalesRepDocumentAuthorizationRequirement(File file, string permission)
        : base(permission)
    {
        File = file;
    }

    public File File { get; }
}
