using VirtoCommerce.FileExperienceApi.Core.Models;
using VirtoCommerce.Platform.Security.Authorization;

namespace VirtoCommerce.SalesRep.ExperienceApi.Authorization;

// Carries the file so the handler can require the library-document claim (owner stamp) on reads; null for list-level checks.
public class SalesRepDocumentAuthorizationRequirement : PermissionAuthorizationRequirement
{
    public SalesRepDocumentAuthorizationRequirement(File file, string permission)
        : base(permission)
    {
        File = file;
    }

    public File File { get; }
}
