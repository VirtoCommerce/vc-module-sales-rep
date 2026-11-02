using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Security.Authorization;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.Infrastructure;

/// <summary>
/// What Platform.Web's PermissionAuthorizationPolicyProvider does for <c>[Authorize("permission:name")]</c> on the
/// module controllers: a policy name that is not a static policy is a permission requirement, decided by the
/// platform's own DefaultPermissionAuthorizationHandler (administrators pass; otherwise the principal's permission
/// claims decide). The platform maps registered permissions only; this host maps any name, so a misspelled
/// permission denies the request instead of failing to resolve a policy.
/// </summary>
internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
        => await base.GetPolicyAsync(policyName)
           ?? new AuthorizationPolicyBuilder()
               .AddRequirements(new PermissionAuthorizationRequirement(policyName))
               .Build();
}
