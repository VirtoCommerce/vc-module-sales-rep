using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.Web;
using VirtoCommerce.XCart.Core.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests
{
    // The "Customer" wishlist scope reaches the platform through one registration line and nothing else. Without
    // this the scope can be dropped from Module.Initialize with every other test still green.
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
    }
}
