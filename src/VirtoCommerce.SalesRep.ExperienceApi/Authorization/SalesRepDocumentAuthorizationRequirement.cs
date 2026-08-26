using VirtoCommerce.Platform.Security.Authorization;

namespace VirtoCommerce.SalesRep.ExperienceApi.Authorization;

public class SalesRepDocumentAuthorizationRequirement : PermissionAuthorizationRequirement
{
    public SalesRepDocumentAuthorizationRequirement(string permission)
        : base(permission)
    {
    }
}
