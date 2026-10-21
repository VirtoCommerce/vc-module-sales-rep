using System;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.Web;
using VirtoCommerce.XCart.Core.Services;
using VirtoCommerce.XCart.Data.Authorization;
using VirtoCommerce.XCart.Data.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests
{
    // The "Customer" scope reaches the platform through one registration line - nothing else would catch its loss.
    [Trait("Category", "Unit")]
    public class ModuleRegistrationTests
    {
        [Fact]
        public void Initialize_RegistersTheCustomerCartSharingScopePolicy()
        {
            var services = new ServiceCollection();

            new Module().Initialize(services);

            services.Should().ContainSingle(x =>
                x.ServiceType == typeof(ICartSharingScopePolicy)
                && x.ImplementationType == typeof(SalesRepCustomerCartSharingScopePolicy));
        }

        // VCST-6476: X-Cart's singleton cart handler holds the policy, so nothing below it may be scoped.
        [Fact]
        public void Initialize_CustomerPolicyHeldBySingleton_PassesScopeValidation()
        {
            var services = new ServiceCollection();
            new Module().Initialize(services);

            // X-Cart's chain as AddXCart registers it.
            services.AddSingleton<IAuthorizationHandler, CanAccessCartAuthorizationHandler>();
            services.AddTransient<ICartSharingService, CartSharingService>();

            // Unused stand-ins: a regression then fails on the scope check, not on a missing service.
            services.AddSingleton<Func<UserManager<ApplicationUser>>>(_ => () => null);
            services.AddSingleton<Func<RoleManager<Role>>>(_ => () => null);
            services.AddSingleton<IPlatformMemoryCache>(_ => null);
            services.AddSingleton<IShoppingCartService>(_ => null);
            services.AddSingleton<ICartAggregateRepository>(_ => null);
            services.AddSingleton<IMemberService>(_ => null);
            services.AddSingleton<IOrganizationMembershipSearchService>(_ => null);

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

            provider.Invoking(x => x.GetServices<IAuthorizationHandler>()).Should().NotThrow();
        }
    }
}
