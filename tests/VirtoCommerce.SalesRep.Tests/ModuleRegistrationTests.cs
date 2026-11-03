using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.Web;
using VirtoCommerce.XCart.Core.Services;
using Xunit;
using TaskManagementSettings = VirtoCommerce.TaskManagement.Core.ModuleConstants.Settings.General;

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

        // The same holds for Call in task-management's built-in TaskTypes list: the seeder's own tests use fresh
        // descriptors, and the test harnesses add Call themselves.
        [Fact]
        public void Initialize_AddsCallToTheBuiltInTaskTypes_AndRegistersTheSeeder()
        {
            var taskTypes = TaskManagementSettings.TaskTypes;
            var original = taskTypes.AllowedValues;
            // A static descriptor: another test's Initialize may already have added Call to it.
            taskTypes.AllowedValues = original.Where(x => !(x as string).EqualsIgnoreCase("Call")).ToArray();

            try
            {
                var services = new ServiceCollection();

                new Module().Initialize(services);

                taskTypes.AllowedValues.Should().Contain("Call");
                services.Should().ContainSingle(x =>
                    x.ServiceType == typeof(ISalesRepTaskTypeSeeder)
                    && x.ImplementationType == typeof(SalesRepTaskTypeSeeder));
            }
            finally
            {
                taskTypes.AllowedValues = original;
            }
        }
    }
}
