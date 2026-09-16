using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.Web;
using VirtoCommerce.XCart.Core.Services;
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
    }
}
