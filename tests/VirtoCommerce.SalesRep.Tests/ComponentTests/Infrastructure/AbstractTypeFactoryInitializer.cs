using System.Runtime.CompilerServices;
using System.Threading;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Data.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Registers the customer AbstractTypeFactory type mappings (ported from CustomerModule PostInitialize) ONCE,
/// at assembly load, via a ModuleInitializer. AbstractTypeFactory uses a non-thread-safe List internally and
/// xunit runs tests in parallel, so these global registrations must happen exactly once and never per-test.
/// </summary>
internal static class AbstractTypeFactoryInitializer
{
    private static int _initialized;

    [ModuleInitializer]
    internal static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return;
        }

        AbstractTypeFactory<Member>.RegisterType<Organization>().MapToType<OrganizationEntity>();
        AbstractTypeFactory<Member>.RegisterType<Contact>().MapToType<ContactEntity>();
        AbstractTypeFactory<Member>.RegisterType<Vendor>().MapToType<VendorEntity>();
        AbstractTypeFactory<Member>.RegisterType<Employee>().MapToType<EmployeeEntity>();

        AbstractTypeFactory<MemberEntity>.RegisterType<ContactEntity>();
        AbstractTypeFactory<MemberEntity>.RegisterType<OrganizationEntity>();
        AbstractTypeFactory<MemberEntity>.RegisterType<VendorEntity>();
        AbstractTypeFactory<MemberEntity>.RegisterType<EmployeeEntity>();

        // Simulates a downstream module extending the dashboard-layout contract with derived types — at the ROOT
        // (DashboardLayout) and a nested collection element (DashboardBlock) — so persistence round-trips prove both.
        AbstractTypeFactory<DashboardLayout>.OverrideType<DashboardLayout, TestExtendedDashboardLayout>();
        AbstractTypeFactory<DashboardBlock>.OverrideType<DashboardBlock, TestExtendedDashboardBlock>();
    }
}
