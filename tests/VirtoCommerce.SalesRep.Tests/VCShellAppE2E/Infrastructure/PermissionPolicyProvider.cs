using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security.Authorization;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.Infrastructure;

/// <summary>
/// Platform.Web's PermissionAuthorizationPolicyProvider (in its Web project, not on NuGet), restated: for
/// <c>[Authorize("permission:name")]</c> on the module controllers, a policy name that is not a static policy is the
/// permission requirement of a REGISTERED permission (the modules' PostInitialize registers theirs), decided by the
/// platform's DefaultPermissionAuthorizationHandler. The platform's policies name its mixed cookie-and-bearer scheme,
/// which is its own host's; here the default scheme, the Identity cookie, authenticates.
/// </summary>
internal sealed class PermissionPolicyProvider(IOptions<Microsoft.AspNetCore.Authorization.AuthorizationOptions> options, IPermissionsRegistrar permissionsRegistrar, IPlatformMemoryCache memoryCache)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
    {
        // Static policies first.
        var policy = await base.GetPolicyAsync(policyName);

        if (policy == null)
        {
            GetPermissionPolicies().TryGetValue(policyName, out policy);
        }

        return policy;
    }

    private Dictionary<string, AuthorizationPolicy> GetPermissionPolicies()
    {
        var cacheKey = CacheKey.With(GetType(), nameof(GetPermissionPolicies));

        return memoryCache.GetOrCreateExclusive(cacheKey, _ =>
        {
            var policies = new Dictionary<string, AuthorizationPolicy>();
            foreach (var permission in permissionsRegistrar.GetAllPermissions())
            {
                policies[permission.Name] = new AuthorizationPolicyBuilder()
                    .AddRequirements(new PermissionAuthorizationRequirement(permission.Name))
                    .Build();
            }

            return policies;
        });
    }
}
